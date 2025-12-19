using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class HumanInteraction : MonoBehaviour
{
    public enum InteractionState
    {
        Interactable,
        NonInteractable
    }
    [Header("当前状态 ")]
    [SerializeField] private InteractionState currentState;
    public bool IsInteractable => currentState == InteractionState.Interactable;//是否可交互
    private void Awake()
    {
        currentState = InteractionState.Interactable;
    }
    // 运行时切换
    public void SetState(InteractionState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }
    public void ToggleState()//ToggleState函数切换状态
    {
        SetState(IsInteractable ? InteractionState.NonInteractable : InteractionState.Interactable);
    }

}