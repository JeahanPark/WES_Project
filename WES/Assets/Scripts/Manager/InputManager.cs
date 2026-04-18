using UnityEngine;
using UnityEngine.InputSystem;
using UniRx;
using System;

public class InputManager : MonoSingleton<InputManager>
{
    [SerializeField] private InputActionAsset m_InputActionAsset;

    private InputActionMap m_PlayerMap;
    private InputActionMap m_UIMap;
    private InputAction m_MoveAction;
    private InputAction m_LookAction;
    private InputAction m_AttackAction;
    private InputAction m_InteractAction;
    private InputAction m_InventoryAction;
    private InputAction m_SubmitAction;

    // 퀵슬롯 액션 (1~8)
    private InputAction[] m_QuickSlotActions = new InputAction[QuickSlotRegistry.SLOT_COUNT];

    // Observable Subjects
    private readonly Subject<Vector2> m_OnMove = new Subject<Vector2>();
    private readonly Subject<Vector2> m_OnLook = new Subject<Vector2>();
    private readonly Subject<Unit> m_OnAttack = new Subject<Unit>();
    private readonly Subject<Unit> m_OnInteract = new Subject<Unit>();
    private readonly Subject<Unit> m_OnInventory = new Subject<Unit>();
    private readonly Subject<Unit> m_OnEnter = new Subject<Unit>();
    private readonly Subject<int> m_OnQuickSlot = new Subject<int>();

    // Public Observables
    public IObservable<Vector2> OnMoveAsObservable => m_OnMove;
    public IObservable<Vector2> OnLookAsObservable => m_OnLook;
    public IObservable<Unit> OnAttackAsObservable => m_OnAttack;
    public IObservable<Unit> OnInteractAsObservable => m_OnInteract;
    public IObservable<Unit> OnInventoryAsObservable => m_OnInventory;
    public IObservable<Unit> OnEnterAsObservable => m_OnEnter;

    /// <summary>
    /// 퀵슬롯 키 입력 (0~7 인덱스 전달)
    /// </summary>
    public IObservable<int> OnQuickSlotAsObservable => m_OnQuickSlot;

    // Current Input Values
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public override void Init()
    {
        base.Init();

        if (m_InputActionAsset == null)
        {
            m_InputActionAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        }

        if (m_InputActionAsset == null)
        {
            GameDebug.LogError("[InputManager] InputActionAsset not found! Please assign it in Inspector or place in Resources folder.");
            return;
        }

        m_PlayerMap = m_InputActionAsset.FindActionMap("Player");
        m_UIMap = m_InputActionAsset.FindActionMap("UI");

        m_MoveAction = m_PlayerMap.FindAction("Move");
        m_LookAction = m_PlayerMap.FindAction("Look");
        m_AttackAction = m_PlayerMap.FindAction("Attack");
        m_InteractAction = m_PlayerMap.FindAction("Interact");
        m_InventoryAction = m_PlayerMap.FindAction("Inventory");
        m_SubmitAction = m_UIMap.FindAction("Submit");

        // 퀵슬롯 액션 바인딩
        SetupQuickSlotActions();

        // Subscribe to Player events
        m_MoveAction.performed += OnMovePerformed;
        m_MoveAction.canceled += OnMoveCanceled;
        m_LookAction.performed += OnLookPerformed;
        m_LookAction.canceled += OnLookCanceled;
        m_AttackAction.performed += OnAttackPerformed;
        m_InteractAction.performed += OnInteractPerformed;
        m_InventoryAction.performed += OnInventoryPerformed;
        m_SubmitAction.performed += OnEnterPerformed;

        m_PlayerMap.Enable();
        m_UIMap.Enable();
    }

    private void SetupQuickSlotActions()
    {
        for (int i = 0; i < QuickSlotRegistry.SLOT_COUNT; i++)
        {
            string actionName = $"QuickSlot{i + 1}";
            m_QuickSlotActions[i] = m_PlayerMap.FindAction(actionName);

            if (m_QuickSlotActions[i] == null)
            {
                GameDebug.LogWarning($"[InputManager] {actionName} action not found in InputActionAsset. Skipping.");
                continue;
            }

            int slotIndex = i; // 클로저용 캡처
            m_QuickSlotActions[i].performed += (_context) => m_OnQuickSlot.OnNext(slotIndex);
        }
    }

    public override void Clear()
    {
        base.Clear();

        if (m_PlayerMap != null)
        {
            m_MoveAction.performed -= OnMovePerformed;
            m_MoveAction.canceled -= OnMoveCanceled;
            m_LookAction.performed -= OnLookPerformed;
            m_LookAction.canceled -= OnLookCanceled;
            m_AttackAction.performed -= OnAttackPerformed;
            m_InteractAction.performed -= OnInteractPerformed;
            m_InventoryAction.performed -= OnInventoryPerformed;
            m_PlayerMap.Disable();
        }

        if (m_UIMap != null)
        {
            m_SubmitAction.performed -= OnEnterPerformed;
            m_UIMap.Disable();
        }

        m_OnMove?.Dispose();
        m_OnLook?.Dispose();
        m_OnAttack?.Dispose();
        m_OnInteract?.Dispose();
        m_OnInventory?.Dispose();
        m_OnEnter?.Dispose();
        m_OnQuickSlot?.Dispose();
    }

    private void OnMovePerformed(InputAction.CallbackContext _context)
    {
        MoveInput = _context.ReadValue<Vector2>();
        m_OnMove.OnNext(MoveInput);
    }

    private void OnMoveCanceled(InputAction.CallbackContext _context)
    {
        MoveInput = Vector2.zero;
        m_OnMove.OnNext(MoveInput);
    }

    private void OnLookPerformed(InputAction.CallbackContext _context)
    {
        LookInput = _context.ReadValue<Vector2>();
        m_OnLook.OnNext(LookInput);
    }

    private void OnLookCanceled(InputAction.CallbackContext _context)
    {
        LookInput = Vector2.zero;
        m_OnLook.OnNext(LookInput);
    }

    private void OnAttackPerformed(InputAction.CallbackContext _context)
    {
        m_OnAttack.OnNext(Unit.Default);
    }

    private void OnInteractPerformed(InputAction.CallbackContext _context)
    {
        m_OnInteract.OnNext(Unit.Default);
    }

    private void OnInventoryPerformed(InputAction.CallbackContext _context)
    {
        m_OnInventory.OnNext(Unit.Default);
    }

    private void OnEnterPerformed(InputAction.CallbackContext _context)
    {
        m_OnEnter.OnNext(Unit.Default);
    }

    public void EnablePlayerInput()
    {
        m_PlayerMap?.Enable();
    }

    public void DisablePlayerInput()
    {
        m_PlayerMap?.Disable();
    }

    public void EnableUIInput()
    {
        m_UIMap?.Enable();
    }

    public void DisableUIInput()
    {
        m_UIMap?.Disable();
    }
}
