using UnityEngine;

// 传送门触发器，检测玩家穿越并执行传送
public class PortalTeleporter : MonoBehaviour
{
    private Door parentDoor;
    private Transform player;
    private bool hasPassedThrough = false;

    public void Initialize(Door door)
    {
        parentDoor = door;
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
        if (player == null || parentDoor == null || parentDoor.targetLevel == null || hasPassedThrough) return;

        Vector3 playerOffset = player.position - parentDoor.transform.position;
        float dotProduct = Vector3.Dot(-parentDoor.transform.forward, playerOffset);
        
        float distanceFromPlane = Mathf.Abs(dotProduct);
                
        if (dotProduct < -0.2f && distanceFromPlane < 3.0f && parentDoor.canEnter) 
        {
            parentDoor.Teleport(player);
            hasPassedThrough = true;
        }
    }
}
