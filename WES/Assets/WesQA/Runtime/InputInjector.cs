using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WesQA
{
    /// <summary>Poco 정규화 좌표(top-left 0~1)를 스크린 픽셀로 변환해
    /// EventSystem 합성 포인터 입력을 주입한다. 모든 호출은 메인스레드(서버 펌프)에서 실행됨.</summary>
    public static class InputInjector
    {
        private static Vector2 ToScreen(double x, double y)
        {
            return new Vector2((float)(x * Screen.width), (float)((1.0 - y) * Screen.height));
        }

        private static GameObject Raycast(Vector2 screenPos, out RaycastResult hit)
        {
            hit = default;
            if (EventSystem.current == null) return null;
            var ev = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ev, results);
            if (results.Count == 0) return null;
            hit = results[0];
            return hit.gameObject;
        }

        private static PointerEventData MakePointer(Vector2 pos, RaycastResult hit,
            PointerEventData.InputButton button)
        {
            return new PointerEventData(EventSystem.current)
            {
                position = pos,
                button = button,
                pointerPressRaycast = hit,
                pointerCurrentRaycast = hit,
            };
        }

        public static bool Click(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Left, 1);
        public static bool RClick(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Right, 1);
        public static bool DoubleClick(double x, double y) => DoClick(x, y, PointerEventData.InputButton.Left, 2);

        private static bool DoClick(double x, double y, PointerEventData.InputButton button, int count)
        {
            var pos = ToScreen(x, y);
            var go = Raycast(pos, out var hit);
            if (go == null) return false;
            var ev = MakePointer(pos, hit, button);
            ev.clickCount = count;
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerUpHandler);
            var clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) ?? go;
            ExecuteEvents.Execute(clickTarget, ev, ExecuteEvents.pointerClickHandler);
            return true;
        }

        public static bool KeyEvent(string keycode)
        {
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null) return false;
            var ev = new BaseEventData(EventSystem.current);
            if (keycode == "enter" || keycode == "submit")
                return ExecuteEvents.Execute(sel, ev, ExecuteEvents.submitHandler);
            if (keycode == "escape" || keycode == "cancel")
                return ExecuteEvents.Execute(sel, ev, ExecuteEvents.cancelHandler);
            return false;
        }
    }
}
