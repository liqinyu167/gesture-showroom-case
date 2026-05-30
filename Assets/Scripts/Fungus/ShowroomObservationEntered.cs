using System.Collections;
using UnityEngine;

namespace Fungus
{
    [EventHandlerInfo("Showroom",
                      "Observation Entered",
                      "Executes the block when the specified showroom item enters observation mode.")]
    [AddComponentMenu("")]
    public class ShowroomObservationEntered : EventHandler
    {
        public class ShowroomObservationEnteredEvent
        {
            public ItemObserver Observer { get; }

            public ShowroomObservationEnteredEvent(ItemObserver observer)
            {
                Observer = observer;
            }
        }

        [Tooltip("The showroom item that should trigger this block.")]
        [SerializeField] protected GameObject targetItem;

        [Tooltip("Wait for a number of frames before executing the block.")]
        [SerializeField] protected int waitFrames = 1;

        protected EventDispatcher eventDispatcher;

        protected virtual void OnEnable()
        {
            TryRegisterEventDispatcher();
        }

        protected virtual void OnDisable()
        {
            UnregisterEventDispatcher();
        }

        protected virtual void OnObservationEnteredEvent(ShowroomObservationEnteredEvent evt)
        {
            if (evt?.Observer == null)
            {
                return;
            }

            if (targetItem == null || evt.Observer.gameObject != targetItem)
            {
                return;
            }

            StartCoroutine(DoExecuteBlock());
        }

        protected virtual IEnumerator DoExecuteBlock()
        {
            var count = Mathf.Max(waitFrames, 0);
            while (count > 0)
            {
                count--;
                yield return new WaitForEndOfFrame();
            }

            ExecuteBlock();
        }

        public override string GetSummary()
        {
            return targetItem != null ? targetItem.name : "No Showroom Item";
        }

        protected virtual void Update()
        {
            if (eventDispatcher == null)
            {
                TryRegisterEventDispatcher();
            }
        }

        protected virtual void TryRegisterEventDispatcher()
        {
            if (eventDispatcher != null)
            {
                return;
            }

            var fungusManager = FungusManager.Instance;
            if (fungusManager == null || fungusManager.EventDispatcher == null)
            {
                return;
            }

            eventDispatcher = fungusManager.EventDispatcher;
            eventDispatcher.AddListener<ShowroomObservationEnteredEvent>(OnObservationEnteredEvent);
        }

        protected virtual void UnregisterEventDispatcher()
        {
            if (eventDispatcher == null)
            {
                return;
            }

            eventDispatcher.RemoveListener<ShowroomObservationEnteredEvent>(OnObservationEnteredEvent);
            eventDispatcher = null;
        }
    }
}
