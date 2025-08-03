using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class RagdollController : NetworkBehaviour
{
    [Header("Thresholds")]
    [SerializeField] private float fallSpeedThreshold = -8f;
    [SerializeField] private float hitForceThreshold = 5f;
    [SerializeField] private float reverseBlendTime = 0.5f;

    [Header("References")]
    [SerializeField] private Rigidbody mainBody;
    [SerializeField] private Collider capsuleCollider;
    [SerializeField] private Animator animator;

    // ����������� � Awake()
    private List<Transform> animatorBones;
    private List<Rigidbody> boneBodies;
    private List<Collider> boneColliders;

    private bool isRagdolled = false;
    private bool reverseQueued = false;

    void Awake()
    {
        // 1) �������� ��� ����������-����� (����� �����)
        boneBodies = GetComponentsInChildren<Rigidbody>()
                        .Where(rb => rb != mainBody)
                        .ToList();

        // 2) ������ ������������ ������ ����������� ���������
        animatorBones = new List<Transform>(boneBodies.Count);
        foreach (var rb in boneBodies)
        {
            // ������������, ��� ��� Transform ��������� � HumanBodyBones enum
            if (System.Enum.TryParse<HumanBodyBones>(rb.name, out var hb))
            {
                animatorBones.Add(animator.GetBoneTransform(hb));
            }
            else
            {
                // ���� ����� ������������� � ������ ��������� ���� Transform,
                // ��� ����������, �� �����������
                animatorBones.Add(rb.transform);
            }
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer || isRagdolled) return;

        // 1) ���������� ragdoll �� �������� �������
        if (mainBody.linearVelocity.y < fallSpeedThreshold)
            //CmdEnableRagdoll();
            if (NetworkClient.active)
                CmdEnableRagdoll();
            else
                RpcEnableRagdoll();
    }

    void OnCollisionEnter(Collision col)
    {
        if (!isLocalPlayer || isRagdolled) return;

        // 2) ���������� ragdoll �� �������� �����
        if (col.relativeVelocity.magnitude > hitForceThreshold)
            //CmdEnableRagdoll();
            if (NetworkClient.active)
                CmdEnableRagdoll();
            else
                RpcEnableRagdoll();
    }

    void Update()
    {
        if (!isLocalPlayer || !NetworkClient.active) return;

        // 3) �������� �������
        if (!isRagdolled && Input.GetKeyDown(KeyCode.K))
            CmdEnableRagdoll();
        if (isRagdolled && Input.GetKeyDown(KeyCode.L))
            CmdReverseRagdoll();

        // 4) �������������� ������ ��������� �� ��������� ������
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

        // ��������� �������� � ���������+������ �����
        animator.enabled = false;
        capsuleCollider.enabled = false;
        mainBody.isKinematic = true;

        // �������� ������ �� ������
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
        // 1) ������ ������� ��������� ������� ������
        var startRots = boneBodies.Select(b => b.transform.localRotation).ToArray();

        // 2) ��������� ������ ������
        foreach (var b in boneBodies)
            b.isKinematic = true;

        // 3) ����� ���
        float t = 0f;
        while (t < reverseBlendTime)
        {
            t += Time.deltaTime;
            float blend = t / reverseBlendTime;
            for (int i = 0; i < boneBodies.Count; i++)
            {
                var bone = boneBodies[i].transform;
                var target = animatorBones[i].localRotation;
                bone.localRotation = Quaternion.Slerp(startRots[i], target, blend);
            }
            yield return null;
        }

        // 4) �������������� Animator � ����������
        animator.enabled = true;
        capsuleCollider.enabled = true;
        mainBody.isKinematic = false;
        isRagdolled = false;
        reverseQueued = false;

        // ��������� ������ ������
        foreach (var c in boneColliders)
            c.enabled = false;
    }
}