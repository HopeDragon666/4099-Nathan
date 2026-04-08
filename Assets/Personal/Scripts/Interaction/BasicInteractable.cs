using EditorAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace Interaction
{
    public class BasicInteractable : MonoBehaviour
    {
        [SerializeField, Title("Interaction")] string interactionName = "Interactable";

        [SerializeField] bool oneShot;

        [SerializeField] UnityEvent onInteracted;

        bool _hasBeenInteracted;

        public string InteractionName => interactionName;

        public virtual bool CanInteract(GameObject interactor)
        {
            return !oneShot || !_hasBeenInteracted;
        }

        public virtual void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            // Mark one-shot interactions as consumed before invoking events to prevent repeated triggers.
            _hasBeenInteracted = true;
            onInteracted?.Invoke();
        }
    }
}
