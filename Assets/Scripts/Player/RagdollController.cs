using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using System.Runtime.CompilerServices;

public class RagdollController : NetworkBehaviour
{
    [Header("Thresholds")]
    [SerializeField] private float fallSpeedThreshold = -8f;
    [SerializeField] private float hitForceThreshold = 5f;
    [SerializeField] private float reverseBlendTime = 0.5f;
    [SerializeField] private float minFallHeight = 2f;
    [SerializeField] private float minAirTime = 0.6f;
    [SerializeField] private float sideHitImpulse = 8f;

    private float _fallStartY = float.NaN;
    private float _airTime = 0f;

    [Header("References")]
    [SerializeField] private Rigidbody mainBody;
    [SerializeField] private Collider capsuleCollider;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerJump jumpScript;

    // заполн€ютс€ в Awake()
    private List<Transform> animatorBones;
    private List<Rigidbody> boneBodies;
    private List<Collider> boneColliders;

    private bool isRagdolled = false;
    private bool reverseQueued = false;
    bool grounded;
    float vy;
    private Transform hips;
    private Vector3 hipsOffsetLocal;

    public override void OnStartClient()
    {
        base.OnStartClient();

        // если что-то не задано в инспекторе Ч найдЄм на этом же GameObject
        mainBody = mainBody ?? GetComponent<Rigidbody>();
        capsuleCollider = capsuleCollider ?? GetComponent<CapsuleCollider>();
        animator = animator ?? GetComponent<Animator>();

        // теперь собираем все костные тела/коллайдеры
        boneBodies = GetComponentsInChildren<Rigidbody>()
                            .Where(rb => rb != mainBody).ToList();
        boneColliders = GetComponentsInChildren<Collider>()
                            .Where(c => c != capsuleCollider).ToList();
        DisableRagdollImmediate();
    }
    void Awake()
    {
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);

        // локальное смещение бЄдер относительно корн€ в Ђровнойї позе
        hipsOffsetLocal = transform.InverseTransformPoint(hips.position);

        // 1) собираем все риджидбоди-кости (кроме корн€)
        boneBodies = GetComponentsInChildren<Rigidbody>()
                        .Where(rb => rb != mainBody)
                        .ToList();

        // 2) создаЄм параллельный список трансформов аниматора
        animatorBones = new List<Transform>(boneBodies.Count);
        foreach (var rb in boneBodies)
        {
            // ѕредполагаем, что им€ Transform совпадает с HumanBodyBones enum
            if (System.Enum.TryParse<HumanBodyBones>(rb.name, out var hb))
            {
                animatorBones.Add(animator.GetBoneTransform(hb));
            }
            else
            {
                // ≈сли кость нестандартна€ Ч просто добавл€ем саму Transform,
                // или игнорируем, по потребности
                animatorBones.Add(rb.transform);
            }
        }
    }

    void FixedUpdate()
    {
        if (!isServer || isRagdolled) return;

        vy = mainBody.linearVelocity.y;

        grounded = jumpScript.CheckGrounded();
        // отслеживаем старт падени€ и Ђврем€ в воздухеї
        if (!grounded)
        {
            if (float.IsNaN(_fallStartY)) _fallStartY = transform.position.y;
            _airTime += Time.fixedDeltaTime;
        }
        else
        {
            _fallStartY = float.NaN;
            _airTime = 0f;
        }

        // 1) больша€ вертикальна€ скорость + высота падени€
        if (!isRagdolled && vy < fallSpeedThreshold)
        {
            float fallDist = float.IsNaN(_fallStartY) ? 0f : (_fallStartY - transform.position.y);
            if (fallDist >= minFallHeight)
                CmdEnableRagdoll();
        }

        // 2) просто долго в воздухе (сошЄл с уступа)
        if (!isRagdolled && _airTime >= minAirTime)
            CmdEnableRagdoll();
    }

    void OnCollisionEnter(Collision col)
    {
        if (!isServer || isRagdolled) return;

        var impulse = col.impulse.magnitude / Time.fixedDeltaTime;
        Vector3 horizontalImpulse = Vector3.ProjectOnPlane(col.impulse, Vector3.up);
        if (horizontalImpulse.magnitude / Time.fixedDeltaTime >= sideHitImpulse)
            CmdEnableRagdoll();

        // как и раньше: общий порог по относительной скорости
        if (col.relativeVelocity.magnitude > hitForceThreshold)
            CmdEnableRagdoll();
    }

    void Update()
    {
        if (!isLocalPlayer || !NetworkClient.active) return;

        // 3) тестовые клавиши
        if (!isRagdolled && Input.GetKeyDown(KeyCode.K))
            CmdEnableRagdoll();
        if (isRagdolled && Input.GetKeyDown(KeyCode.L))
            CmdReverseRagdoll();

        // 4) автоматический запуск обратного по остановке костей
        if (isRagdolled && !reverseQueued)
        {
            bool moving = boneBodies.Any(b => b.linearVelocity.sqrMagnitude > 0.01f);
            if (!moving)
            {
                reverseQueued = true;
                CmdReverseRagdoll();
            }
        }
    }

    [Command]
    private void CmdEnableRagdoll()
    {
        RpcEnableRagdoll();
    }

    [ClientRpc]
    private void RpcEnableRagdoll()
    {
        if (isRagdolled) return;
        isRagdolled = true;
        reverseQueued = false;

        // выключаем анимацию и коллайдер+физику корн€
        animator.enabled = false;
        capsuleCollider.enabled = false;
        mainBody.isKinematic = true;

        // включаем физику на кост€х
        foreach (var b in boneBodies)
        {
            b.isKinematic = false;
            b.detectCollisions = true;
        }
        foreach (var c in boneColliders)
            c.enabled = true;
    }

    [Command]
    private void CmdReverseRagdoll()
    {
        RpcReverseRagdoll();
    }

    [ClientRpc]
    private void RpcReverseRagdoll()
    {
        StartCoroutine(ReverseRoutine());
    }

    private IEnumerator ReverseRoutine()
    {
        // 1) снимаем стартовые локальные повороты (как у теб€)
        var startRots = boneBodies.Select(b => b.transform.localRotation).ToArray();

        // 2) замораживаем физику костей
        foreach (var b in boneBodies) b.isKinematic = true;

        // 3) (опционально) короткий slerp к позе аниматора Ч можно оставить как у теб€
        float t = 0f;
        while (t < reverseBlendTime)
        {
            t += Time.deltaTime;
            float k = t / reverseBlendTime;
            for (int i = 0; i < boneBodies.Count; i++)
            {
                var bone = boneBodies[i].transform;
                var target = animatorBones[i].localRotation;
                bone.localRotation = Quaternion.Slerp(startRots[i], target, k);
            }
            yield return null;
        }

        // 4) ставим root туда, где реально лежит ragdoll (по бЄдрам)
        SnapRootToHips();

        // 5) переносим среднюю скорость костей в корневое тело
        mainBody.linearVelocity = GetRagdollAverageVelocity();

        // 6) включаем аниматор и играем get-up без RootMotion
        animator.applyRootMotion = false;
        animator.enabled = true;
        capsuleCollider.enabled = true;
        mainBody.isKinematic = false;

        // выбрать анимацию подъЄма по Ђлицом вниз/вверхї
        bool faceDown = Vector3.Dot(hips.forward, Vector3.up) < 0f; // груба€ эвристика
        animator.CrossFade(faceDown ? "GetUp_Front" : "GetUp_Back", 0.05f);

        isRagdolled = false;
        reverseQueued = false;

        // 7) отключаем физ.коллайдеры костей
        foreach (var c in boneColliders) c.enabled = false;

        // (опц.) через пару кадров вернуть applyRootMotion, если он тебе нужен
        yield return null;
    }
    private void DisableRagdollImmediate()
    {
        // ¬ключаем анимацию и коллайдер/физику корн€
        animator.enabled = true;
        capsuleCollider.enabled = true;
        mainBody.isKinematic = false;

        // ƒелаем все кости кинематическими и отключаем их коллайдеры
        foreach (var b in boneBodies)
        {
            b.isKinematic = true;
            b.detectCollisions = false;
        }
        foreach (var c in boneColliders)
            c.enabled = false;

        isRagdolled = false;
        reverseQueued = false;
    }
    private void SnapRootToHips()
    {
        // где должны быть корень, если бЄдра сейчас вот тут
        Vector3 worldOffset = transform.TransformVector(hipsOffsetLocal);
        Vector3 targetRootPos = hips.position - worldOffset;

        // ѕоворот: берЄм Ђгоризонтальныйї yaw из бЄдер, чтобы не было жЄсткого кручени€
        Vector3 forwardFlat = Vector3.ProjectOnPlane(hips.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 1e-4f) forwardFlat = transform.forward;
        Quaternion targetRootRot = Quaternion.LookRotation(forwardFlat, Vector3.up);

        // ставим корень
        transform.SetPositionAndRotation(targetRootPos, targetRootRot);
    }
    private Vector3 GetRagdollAverageVelocity()
    {
        if (boneBodies == null || boneBodies.Count == 0) return Vector3.zero;
        Vector3 v = Vector3.zero;
        for (int i = 0; i < boneBodies.Count; i++)
            v += boneBodies[i].linearVelocity;
        return v / boneBodies.Count;
    }
}