using UnityEngine;

// 画框传送触发器，检测玩家穿越并执行传送
public class PortalTeleporter : MonoBehaviour
{
    private Frame parentFrame;
    private Transform player;
    private bool hasPassedThrough = false;

    public void Initialize(Frame frame)
    {
        parentFrame = frame;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.transform;
            hasPassedThrough = false;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform == player)
        {
            player = null;
            hasPassedThrough = false;
        }
    }

    void LateUpdate()
    {
        if (player == null || parentFrame == null || parentFrame.targetLevel == null || hasPassedThrough) return;

        Vector3 playerOffset = player.position - parentFrame.transform.position;
        float dotProduct = Vector3.Dot(parentFrame.transform.forward, playerOffset);
        float distanceFromPlane = Mathf.Abs(dotProduct);
                        
        if (dotProduct < -0.0f && distanceFromPlane < 1.0f && parentFrame.canEnter)
        {
            parentFrame.transform.Find("BackSideCollider").gameObject.SetActive(false);
            parentFrame.Teleport(player);
            hasPassedThrough = true;
        }
    }
}