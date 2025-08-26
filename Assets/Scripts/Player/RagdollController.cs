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
    [SerializeField] private float stepOffHeight = 0.6f; // порог для "сошёл с уступа"
    private float maxFallHeight = 0f; // накопитель максимальной высоты падения

    private float _fallStartY = float.NaN;
    private float _airTime = 0f;

    [Header("Ragdoll settings")]
    [SerializeField] private float minRagdollTime = 0.7f;   // минимум столько лежим
    [SerializeField] private float stopCheckDelay = 0.15f;  // пауза перед проверкой "кости остановились"
    [SerializeField] private float standUpMaxSlope = 50f; // градусы

    private bool ragdollArmed = false;      // чтобы не дергать Cmd по 10 раз
    private float ragdollSince = -1f;       // время начала регдолла

    [Header("References")]
    [SerializeField] private Rigidbody mainBody;
    [SerializeField] private Collider capsuleCollider;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerJump jumpScript;

    // заполняются в Awake()
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

        // если что-то не задано в инспекторе — найдём на этом же GameObject
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

        // локальное смещение бёдер относительно корня в «ровной» позе
        hipsOffsetLocal = transform.InverseTransformPoint(hips.position);

        // 1) собираем все риджидбоди-кости (кроме корня)
        boneBodies = GetComponentsInChildren<Rigidbody>()
                        .Where(rb => rb != mainBody)
                        .ToList();

        // 2) создаём параллельный список трансформов аниматора
        animatorBones = new List<Transform>(boneBodies.Count);
        foreach (var rb in boneBodies)
        {
            // Предполагаем, что имя Transform совпадает с HumanBodyBones enum
            if (System.Enum.TryParse<HumanBodyBones>(rb.name, out var hb))
            {
                animatorBones.Add(animator.GetBoneTransform(hb));
            }
            else
            {
                // Если кость нестандартная — просто добавляем саму Transform,
                // или игнорируем, по потребности
                animatorBones.Add(rb.transform);
            }
        }
    }

    void FixedUpdate()
    {
        if (!isServer || isRagdolled) return;

        vy = mainBody.linearVelocity.y;

        bool prevGrounded = grounded;
        grounded = jumpScript.CheckGrounded();
        // отслеживаем старт падения и «время в воздухе»
        if (!grounded)
        {
            if (prevGrounded) { _fallStartY = transform.position.y; maxFallHeight = 0f; _airTime = 0f; }
            _airTime += Time.fixedDeltaTime;

            // обновляем максимум потери высоты за этот полёт
            float h = _fallStartY - transform.position.y;
            if (h > maxFallHeight) maxFallHeight = h;
        }
        else
        {
            _fallStartY = float.NaN;
            _airTime = 0f;
            maxFallHeight = 0f;
        }

        // 1) большая вертикальная скорость + высота падения
        if (!isRagdolled && vy < fallSpeedThreshold)
        {
            float fallDist = float.IsNaN(_fallStartY) ? 0f : (_fallStartY - transform.position.y);
            if (fallDist >= minFallHeight)
                if (!isRagdolled && !ragdollArmed) { ragdollArmed = true; CmdEnableRagdoll(); }
        }

        // 2) просто долго в воздухе (сошёл с уступа)
        if (!isRagdolled && !ragdollArmed)
        {
            bool highFall = (_airTime > 0.1f) && (vy < fallSpeedThreshold) && (maxFallHeight >= minFallHeight);
            bool steppedOff = (_airTime >= minAirTime) && (maxFallHeight >= stepOffHeight);

            if (highFall || steppedOff)
            {
                ragdollArmed = true;
                CmdEnableRagdoll();
            }
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (!isServer || isRagdolled || ragdollArmed) return;

        // горизонтальный импульс (снос вбок)
        Vector3 horizontalImpulse = Vector3.ProjectOnPlane(col.impulse, Vector3.up);
        float horizForce = horizontalImpulse.magnitude / Time.fixedDeltaTime;

        bool strongHit = (horizForce >= sideHitImpulse) || (col.relativeVelocity.magnitude >= hitForceThreshold);
        if (strongHit)
        {
            ragdollArmed = true;
            CmdEnableRagdoll();
        }
    }

    void Update()
    {
        if (!isLocalPlayer || !NetworkClient.active) return;

        // 3) тестовые клавиши
        if (!isRagdolled && !ragdollArmed && Input.GetKeyDown(KeyCode.K))
            CmdEnableRagdoll();
        if (isRagdolled && Input.GetKeyDown(KeyCode.L))
            CmdReverseRagdoll();

        // 4) автоматический запуск обратного по остановке костей
        if (isRagdolled && !reverseQueued && Time.time - ragdollSince >= minRagdollTime)
        {
            if (!GroundTooSteep(out _))
            {
                bool moving = boneBodies.Any(b => b.linearVelocity.sqrMagnitude > 0.01f);
                if (!moving)
                {
                    reverseQueued = true;
                    CmdReverseRagdoll();
                }
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
        ragdollArmed = false;
        reverseQueued = false;
        ragdollSince = Time.time;

        // выключаем анимацию и коллайдер+физику корня
        animator.enabled = false;
        capsuleCollider.enabled = false;
        mainBody.isKinematic = true;

        // включаем физику на костях
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
        if (GroundTooSteep(out _)) { isRagdolled = true; reverseQueued = false; yield break; }
        // 1) снимаем стартовые локальные повороты (как у тебя)
        var startRots = boneBodies.Select(b => b.transform.localRotation).ToArray();

        // 2) замораживаем физику костей
        foreach (var b in boneBodies) b.isKinematic = true;

        // 3) (опционально) короткий slerp к позе аниматора — можно оставить как у тебя
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

        // a) раскладываем корень в нужную позицию/поворот
        SnapRootToHips();

        // b) включаем коллайдер и делаем RB динамическим
        capsuleCollider.enabled = true;
        mainBody.isKinematic = false;

        // c) на следующее FixedUpdate кладём импульс (иначе попадём в кадр, когда RB ещё kinematic)
        var avgV = GetRagdollAverageVelocity();
        StartCoroutine(ApplyImpulseNextFixed(mainBody, avgV));

        // d) включаем аниматор
        animator.applyRootMotion = false; // или true, если ты хочешь тянуть rootMotion
        animator.enabled = true;

        // выбрать анимацию подъёма по «лицом вниз/вверх»
        bool faceDown = Vector3.Dot(hips.forward, Vector3.up) < 0f; // грубая эвристика
        animator.CrossFade(faceDown ? "GetUp_Front" : "GetUp_Back", 0.05f);

        isRagdolled = false;
        reverseQueued = false;

        // 7) отключаем физ.коллайдеры костей
        foreach (var c in boneColliders) c.enabled = false;

        // (опц.) через пару кадров вернуть applyRootMotion, если он тебе нужен
        yield return null;
    }
    private IEnumerator ApplyImpulseNextFixed(Rigidbody rb, Vector3 avgV)
    {
        yield return new WaitForFixedUpdate();
        // мягкий старт: ограничим горизонтальную скорость, вертикаль оставим (чтобы не «влетать» в склон)
        Vector3 horiz = Vector3.ProjectOnPlane(avgV, Vector3.up);
        Vector3 vel = Vector3.ClampMagnitude(horiz, 3.5f) + Vector3.up * Mathf.Max(0f, avgV.y);
        // вместо прямой установки можно импульс — ещё плавнее:
        rb.linearVelocity = vel; // или rb.AddForce(vel * rb.mass, ForceMode.Impulse);
    }
    private void DisableRagdollImmediate()
    {
        // Включаем анимацию и коллайдер/физику корня
        animator.enabled = true;
        capsuleCollider.enabled = true;
        mainBody.isKinematic = false;

        // Делаем все кости кинематическими и отключаем их коллайдеры
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
        // где должны быть корень, если бёдра сейчас вот тут
        Vector3 worldOffset = transform.TransformVector(hipsOffsetLocal);
        Vector3 targetRootPos = hips.position - worldOffset;

        // Поворот: берём «горизонтальный» yaw из бёдер, чтобы не было жёсткого кручения
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
    private bool GroundTooSteep(out RaycastHit hit)
    {
        Vector3 origin = hips.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle > standUpMaxSlope;
        }
        return true; // если не нашли пол — считаем круто/опасно
    }
}