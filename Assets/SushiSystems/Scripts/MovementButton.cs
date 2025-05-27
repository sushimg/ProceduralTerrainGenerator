using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class MovementButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float doubleClickTime = 0.3f;

    [SerializeField] private UnityEvent OnSingleClick;
    [SerializeField] private UnityEvent OnDoubleClick;
    [SerializeField] private UnityEvent OnPointerDownEvent;
    [SerializeField] private UnityEvent OnPointerUpEvent;

    [SerializeField] private int clickCount = 0;
    [SerializeField] private float timer = 0f;

    void Update()
    {
        HandleClick();
    }

    #region Handle Clicks

    private void HandleClick()
    {
        if (clickCount > 0)
        {
            timer += Time.deltaTime;

            if (timer > doubleClickTime)
            {
                if (clickCount == 1)
                    OnSingleClick.Invoke();

                clickCount = 0;
                timer = 0f;
            }
        }
    }

    #endregion

    #region Events

    public void OnPointerClick(PointerEventData eventData)
    {
        clickCount++;

        if (clickCount == 1)
        {
            timer = 0f;
        }
        else if (clickCount == 2)
        {
            OnDoubleClick.Invoke();
            clickCount = 0;
            timer = 0f;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnPointerDownEvent.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnPointerUpEvent.Invoke();
    }

    #endregion
}
