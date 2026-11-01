using UnityEngine;

namespace Parallax
{
    /// <summary>
    /// A GUILayout window whose content scrolls once the window reaches its maximum height. The window widens to fit the scrollbar beside the content.
    /// </summary>
    public class ScrollableWindow
    {
        const float scrollAreaHeight = 100000;

        public Rect rect;

        readonly float baseWidth;
        readonly float maxHeight;
        readonly string inputLockID;
        readonly GUIStyle contentStyle;

        bool needsResize = true;
        bool scrollbarVisible = false;
        bool inputLocked = false;

        Vector2 scrollPosition = Vector2.zero;
        float scrollViewTop = 0;
        float scrollViewHeight = 0;
        float scrollContentHeight = 0;

        public ScrollableWindow(Rect rect, float maxHeightToWidth, string inputLockID)
        {
            this.rect = rect;
            this.inputLockID = inputLockID;
            baseWidth = rect.width;
            maxHeight = Mathf.Min(rect.width * maxHeightToWidth, Screen.height);

            // The content's outer margins are lost when it's measured on its own, so pad by the button margin instead
            contentStyle = new GUIStyle();
            contentStyle.padding = new RectOffset(0, 0, HighLogic.Skin.button.margin.top, HighLogic.Skin.button.margin.bottom);
        }
        public void Draw(int id, GUI.WindowFunction drawFunction, string title)
        {
            // Zero height lets the window shrink to fit its content, but only on layout or dragging the window breaks
            if (needsResize && Event.current.type == EventType.Layout)
            {
                rect.width = baseWidth;
                rect.height = 0;
                needsResize = false;
            }
            rect = GUILayout.Window(id, rect, drawFunction, title, HighLogic.Skin.window);
        }
        /// <summary>
        /// Stops the camera zooming while scrolling the window. Call every frame.
        /// </summary>
        public void UpdateInputLock(bool windowShown, bool forceLock = false)
        {
            Vector2 mousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            SetInputLock(forceLock || (windowShown && rect.Contains(mousePosition)));
        }
        public void ReleaseInputLock()
        {
            SetInputLock(false);
        }
        void SetInputLock(bool locked)
        {
            if (locked == inputLocked)
            {
                return;
            }

            if (locked)
            {
                InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS, inputLockID);
            }
            else
            {
                InputLockManager.RemoveControlLock(inputLockID);
            }
            inputLocked = locked;
        }
        /// <summary>
        /// Begins the scrolling content. Anything drawn before this stays pinned above it.
        /// </summary>
        public void BeginScrollView()
        {
            float maxScrollHeight = maxHeight - scrollViewTop - HighLogic.Skin.window.padding.bottom;
            bool needsScrollbar = scrollContentHeight > maxScrollHeight;
            float newScrollViewHeight = Mathf.Min(scrollContentHeight, maxScrollHeight);
            if (newScrollViewHeight != scrollViewHeight)
            {
                scrollViewHeight = newScrollViewHeight;
                needsResize = true;
            }

            // The content keeps a fixed width and the window widens to fit the scrollbar beside it
            float contentWidth = baseWidth - HighLogic.Skin.window.padding.horizontal;
            float scrollViewWidth = contentWidth;
            if (needsScrollbar)
            {
                scrollViewWidth += HighLogic.Skin.verticalScrollbar.fixedWidth + HighLogic.Skin.verticalScrollbar.margin.left;
            }
            if (needsScrollbar != scrollbarVisible)
            {
                scrollbarVisible = needsScrollbar;
                needsResize = true;
            }

            Rect scrollViewRect = GUILayoutUtility.GetRect(scrollViewWidth, scrollViewHeight);
            if (Event.current.type == EventType.Repaint)
            {
                scrollViewTop = scrollViewRect.y;
            }

            // Clamping the content to the view when not scrolling stops Unity showing the scrollbar for a frame while the window resizes
            float contentHeight = needsScrollbar ? scrollContentHeight : Mathf.Min(scrollContentHeight, scrollViewRect.height);
            Rect contentRect = new Rect(0, 0, contentWidth, contentHeight);

            // The scrollbar thumb and buttons are looked up by name from the active skin
            GUISkin previousSkin = GUI.skin;
            GUI.skin = HighLogic.Skin;
            scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, contentRect, false, needsScrollbar, GUIStyle.none, HighLogic.Skin.verticalScrollbar);
            GUI.skin = previousSkin;

            GUILayout.BeginArea(new Rect(0, 0, contentWidth, scrollAreaHeight));
            GUILayout.BeginVertical(contentStyle, GUILayout.ExpandHeight(false));
        }
        /// <summary>
        /// Ends the scrolling content. Anything drawn after this stays pinned below it.
        /// </summary>
        public void EndScrollView()
        {
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                scrollContentHeight = GUILayoutUtility.GetLastRect().height;
            }
            GUILayout.EndArea();
            GUI.EndScrollView();
        }
    }

    /// <summary>
    /// Indents a collapsible menu's content and draws a bar down its left edge, showing which menu the content belongs to
    /// </summary>
    public static class GUISection
    {
        const int indent = 14;
        const float barOffset = 4;
        const float barWidth = 2;
        const float spacing = 6;

        static GUIStyle style;

        public static void Begin()
        {
            if (style == null)
            {
                style = new GUIStyle();
                style.padding = new RectOffset(indent, 0, 0, 0);
            }
            GUILayout.BeginVertical(style);
        }
        public static void End()
        {
            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                Rect section = GUILayoutUtility.GetLastRect();
                Rect bar = new Rect(section.x + barOffset, section.y, barWidth, section.height);

                Color previousColor = GUI.color;
                GUI.color = HighLogic.Skin.label.normal.textColor;
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = previousColor;
            }

            GUILayout.Space(spacing);
        }
    }
}
