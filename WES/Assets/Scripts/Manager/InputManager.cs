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
    private InputAction m_Key1Action;
    private InputAction m_Key2Action;
    private InputAction m_SubmitAction;

    // Observable Subjects
    private readonly Subject<Vector2> m_OnMove = new Subject<Vector2>();
    private readonly Subject<Vector2> m_OnLook = new Subject<Vector2>();
    private readonly Subject<Unit> m_OnAttack = new Subject<Unit>();
    private readonly Subject<Unit> m_OnKey1 = new Subject<Unit>();
    private readonly Subject<Unit> m_OnKey2 = new Subject<Unit>();
    private readonly Subject<Unit> m_OnKey3 = new Subject<Unit>();
    private readonly Subject<Unit> m_OnEnter = new Subject<Unit>();

    // Public Observables
    public IObservable<Vector2> OnMoveAsObservable => m_OnMove;
    public IObservable<Vector2> OnLookAsObservable => m_OnLook;
    public IObservable<Unit> OnAttackAsObservable => m_OnAttack;
    public IObservable<Unit> OnKey1AsObservable => m_OnKey1;
    public IObservable<Unit> OnKey2AsObservable => m_OnKey2;
    public IObservable<Unit> OnKey3AsObservable => m_OnKey3;
    public IObservable<Unit> OnEnterAsObservable => m_OnEnter;

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
            Debug.LogError("[InputManager] InputActionAsset not found! Please assign it in Inspector or place in Resources folder.");
            return;
        }

        // Get Action Maps
        m_PlayerMap = m_InputActionAsset.FindActionMap("Player");
        m_UIMap = m_InputActionAsset.FindActionMap("UI");

        // Get Player Actions
        m_MoveAction = m_PlayerMap.FindAction("Move");
        m_LookAction = m_PlayerMap.FindAction("Look");
        m_AttackAction = m_PlayerMap.FindAction("Attack");
        m_Key1Action = m_PlayerMap.FindAction("Previous");
        m_Key2Action = m_PlayerMap.FindAction("Next");

        // Get UI Actions
        m_SubmitAction = m_UIMap.FindAction("Submit");

        // Subscribe to Player events
        m_MoveAction.performed += OnMovePerformed;
        m_MoveAction.canceled += OnMoveCanceled;

        m_LookAction.performed += OnLookPerformed;
        m_LookAction.canceled += OnLookCanceled;

        m_AttackAction.performed += OnAttackPerformed;
        m_Key1Action.performed += OnKey1Performed;
        m_Key2Action.performed += OnKey2Performed;

        // Subscribe to UI events
        m_SubmitAction.performed += OnEnterPerformed;

        // Enable Input
        m_PlayerMap.Enable();
        m_UIMap.Enable();
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
            m_Key1Action.performed -= OnKey1Performed;
            m_Key2Action.performed -= OnKey2Performed;

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
        m_OnKey1?.Dispose();
        m_OnKey2?.Dispose();
        m_OnKey3?.Dispose();
        m_OnEnter?.Dispose();
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

    private void OnKey1Performed(InputAction.CallbackContext _context)
    {
        m_OnKey1.OnNext(Unit.Default);
    }

    private void OnKey2Performed(InputAction.CallbackContext _context)
    {
        m_OnKey2.OnNext(Unit.Default);
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
