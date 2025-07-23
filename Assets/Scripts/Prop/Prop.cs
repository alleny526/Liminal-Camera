using UnityEngine;

public abstract class Prop : MonoBehaviour
{
    public bool canCapture = true;

    public GameObject prefab;
    
    public virtual void Interact(GameObject interactor) { }
}
