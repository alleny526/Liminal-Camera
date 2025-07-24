using UnityEngine;

public abstract class Prop : MonoBehaviour
{
    public bool canCapture = true;

    public GameObject prefab;

    void Awake()
    {
        prefab = this.gameObject;
    }

    public virtual void Interact(GameObject interactor) { }
}
