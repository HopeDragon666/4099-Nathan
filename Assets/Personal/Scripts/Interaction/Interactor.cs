using EditorAttributes;
using Player;
using UnityEngine;

namespace Interaction
{
    [DisallowMultipleComponent]
    public class Interactor : MonoBehaviour
    {
        [SerializeField, Title("References")] Camera interactionCamera;

        [SerializeField] PlayerInputWrapper inputWrapper;

        [SerializeField, Title("Interaction")] float interactionDistance = 4f;

        [SerializeField] LayerMask interactionMask = ~0;

        [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField, Title("Debug")] bool drawDebugRay;

        void Awake()
        {
            if (interactionCamera == null)
            {
                print("[Error] Interactor: Assign an interaction camera in the inspector.");
                enabled = false;
                return;
            }

            if (inputWrapper == null)
            {
                print("[Error] Interactor: Assign a PlayerInputWrapper reference in the inspector.");
                enabled = false;
                return;
            }
        }

        void Update()
        {
            if (inputWrapper == null || interactionCamera == null)
            {
                return;
            }

            if (drawDebugRay)
            {
                Ray debugRay = BuildCenterScreenRay();
                Debug.DrawRay(debugRay.origin, debugRay.direction * interactionDistance, Color.green);
            }

            if (!inputWrapper.WasInteractPressedThisFrame())
            {
                return;
            }

            TryInteract();
        }

        void TryInteract()
        {
            Ray interactionRay = BuildCenterScreenRay();
            if (!Physics.Raycast(interactionRay, out RaycastHit hit, interactionDistance, interactionMask, triggerInteraction))
            {
                return;
            }

            // Searching parents allows collider-only child objects to still route interaction to their root interactable.
            BasicInteractable interactable = hit.collider.GetComponentInParent<BasicInteractable>();
            if (interactable == null)
            {
                return;
            }

            if (!interactable.CanInteract(gameObject))
            {
                return;
            }

            interactable.Interact(gameObject);
        }

        Ray BuildCenterScreenRay()
        {
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            return interactionCamera.ScreenPointToRay(screenCenter);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Auto-wire local references in editor while still letting inspector overrides take priority.
            if (inputWrapper == null)
            {
                inputWrapper = GetComponent<PlayerInputWrapper>();
            }
        }
#endif
    }
}
