using System;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerInteractor : NetworkBehaviour
{
    public event Action OnInteractTargetUpdated;

    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 5f;
    [SerializeField] private Transform holdPos;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Pickup Settings")]
    [SerializeField] private float throwForce = 500f; //force at which the object is thrown at
    private GameObject heldObj; //object which we pick up
    private Rigidbody heldObjRb; //rigidbody of object we pick up


    private Camera playerCamera;

    private IInteractable currentTarget;
    private IHighlighter currentHighlighter;

    public IInteractable GetCurrentInteractable() => currentTarget;

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        // Always raycast to check for highlight
        CheckForInteractable();


        if (heldObj != null) //if player is holding object
        {
            MoveObject(); //keep object position at holdPos
            if (Input.GetKeyDown(KeyCode.Mouse1)) //Mous1 (rightclick) is used to throw, change this if you want another button to be used)
            {
                StopClipping();
                ThrowObject();
            }

        }

        // Interact / Pickup while holding key
        if (Input.GetKeyDown(interactKey))
        {
            if (heldObj == null && currentTarget != null)
            {
                currentTarget.Interact();
            }
        }

        // Drop on release
        if (Input.GetKeyUp(interactKey))
        {
            DropObject();
        }
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        IInteractable interactable = null;
        IHighlighter highlighter = null;

        if (Physics.Raycast(ray, out hit, interactDistance, interactableLayer))
        {
            interactable = hit.collider.GetComponent<IInteractable>();
            highlighter = hit.collider.GetComponentInChildren<IHighlighter>();
        }

        // Only highlight if interactable exists and CanInteract is true
        if (interactable != null && interactable.CanInteract(NetworkManager.Singleton.LocalClientId))
        {
            if (currentTarget != interactable)
            {
                ClearHighlight();

                if (highlighter != null)
                {
                    currentHighlighter = highlighter;
                    currentHighlighter.Highlight(true);
                }

                currentTarget = interactable;
                OnInteractTargetUpdated?.Invoke();
            }
            return;
        }
        else
        {
            if (currentTarget != null)
            {
                ClearHighlight();
                OnInteractTargetUpdated?.Invoke();
            }
        }
    }

    private void ClearHighlight()
    {
        if (currentHighlighter != null)
        {
            currentHighlighter.Highlight(false);
            currentHighlighter = null;
        }
        currentTarget = null;
    }

    public void PickUpObject(GameObject pickUpObj)
    {
        if (heldObj != null) DropObject();

        if (pickUpObj.GetComponent<Rigidbody>()) //make sure the object has a RigidBody
        {
            heldObj = pickUpObj; //assign heldObj to the object that was hit by the raycast (no longer == null)
            heldObjRb = pickUpObj.GetComponent<Rigidbody>(); //assign Rigidbody
            heldObjRb.isKinematic = true;
            heldObjRb.transform.parent = holdPos.transform; //parent object to holdposition

            //make sure object doesnt collide with player, it can cause weird bugs
            Physics.IgnoreCollision(heldObj.GetComponent<Collider>(), GetComponent<Collider>(), true);
        }
    }
    void DropObject()
    {
        if (heldObj == null) return;

        //re-enable collision with player
        Physics.IgnoreCollision(heldObj.GetComponent<Collider>(), GetComponent<Collider>(), false);

        heldObjRb.isKinematic = false;
        heldObj.transform.parent = null; //unparent object
        heldObj = null; //undefine game object
    }

    void MoveObject()
    {
        //keep object position the same as the holdPosition position
        heldObj.transform.position = holdPos.transform.position;
    }

    void ThrowObject()
    {
        //same as drop function, but add force to object before undefining it
        Physics.IgnoreCollision(heldObj.GetComponent<Collider>(), GetComponent<Collider>(), false);
        heldObjRb.isKinematic = false;
        heldObj.transform.parent = null;
        heldObjRb.AddForce(playerCamera.transform.forward * throwForce);
        heldObj = null;
    }
    void StopClipping() //function only called when dropping/throwing
    {
        var clipRange = Vector3.Distance(heldObj.transform.position, transform.position); //distance from holdPos to the camera
        //have to use RaycastAll as object blocks raycast in center screen
        //RaycastAll returns array of all colliders hit within the cliprange
        RaycastHit[] hits;
        hits = Physics.RaycastAll(transform.position, transform.TransformDirection(Vector3.forward), clipRange);
        //if the array length is greater than 1, meaning it has hit more than just the object we are carrying
        if (hits.Length > 1)
        {
            //change object position to camera position 
            heldObj.transform.position = transform.position + new Vector3(0f, -0.5f, 0f); //offset slightly downward to stop object dropping above player 
            //if your player is small, change the -0.5f to a smaller number (in magnitude) ie: -0.1f
        }
    }
}
