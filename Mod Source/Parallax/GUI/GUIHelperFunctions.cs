using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

//
// Parallax GUI helper functions. Barely any of this is my own code. Mostly adapted from https://forum.unity.com/threads/float-input-gui-item.83739/
//
/// <summary>
/// Parallax GUI helper functions for the in-game config menu
/// </summary>
namespace Parallax
{
    public static class GUIHelperFunctions
    {
        private static int activeFloatField = -1;
        private static float activeFloatFieldLastValue = 0;
        private static string activeFloatFieldString = "";

        private static int activeIntField = -1;
        private static int activeIntFieldLastValue = 0;
        private static string activeIntFieldString = "";

        private static int activeVector3Field = -1;
        private static Vector3 activeVector3FieldLastValue = Vector3.zero;
        private static string activeVector3FieldString = "";

        private static int activeColorField = -1;
        private static Color activeColorFieldLastValue = Color.black;
        private static string activeColorFieldString = "";

        private static int activeBoolField = -1;

        private static int activeEnumField = -1;
        private static NoiseType activeEnumFieldLastValue = NoiseType.simplexPerlin;
        private static string activeEnumFieldString = "";

        private const float dropdownOptionHeight = 22;
        private const int dropdownMaxVisibleOptions = 10;

        private static string openDropdownKey = null;
        private static string openDropdownValue = null;
        private static readonly List<string> openDropdownOptions = new List<string>();
        private static Rect openDropdownAnchor;
        private static Rect openDropdownPopupRect;
        private static int openDropdownLastSeenFrame = -1;
        private static Vector2 openDropdownScroll = Vector2.zero;

        private static string selectedDropdownKey = null;
        private static string selectedDropdownValue = null;

        private static GUIStyle dropdownFieldStyle;
        private static GUIStyle dropdownPopupStyle;
        private static GUIStyle dropdownOptionStyle;
        private static GUIStyle dropdownSelectedOptionStyle;
        private static GUIStyle compactButtonStyle;
        private static Texture2D dropdownBackgroundTexture;
        private static Texture2D dropdownHoverTexture;
        private static Texture2D dropdownSelectedTexture;

        /// <summary>
        /// Float Field for ingame purposes. Behaves exactly like UnityEditor.EditorGUILayout.FloatField.
        /// From https://forum.unity.com/threads/float-input-gui-item.83739/
        /// </summary>
        public static float FloatField(float value, out bool valueWasChanged)
        {
            valueWasChanged = false;

            // Get rect and control for this float field for identification
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString()), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.MinWidth(80) });
            int floatFieldID = GUIUtility.GetControlID("FloatField".GetHashCode(), FocusType.Keyboard, pos) + 1;
            if (floatFieldID == 0)
                return value;

            bool recorded = activeFloatField == floatFieldID;
            bool active = floatFieldID == GUIUtility.keyboardControl;

            if (active && recorded && activeFloatFieldLastValue != value)
            { // Value has been modified externally
                activeFloatFieldLastValue = value;
                activeFloatFieldString = value.ToString();
            }

            // Get stored string for the text field if this one is recorded
            string str = recorded ? activeFloatFieldString : value.ToString();

            // pass it in the text field
            string strValue = GUI.TextField(pos, str, HighLogic.Skin.textField);

            // Update stored value if this one is recorded
            if (recorded)
                activeFloatFieldString = strValue;

            // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value

            float newValue;
            bool parsed = float.TryParse(strValue, out newValue);
            if (parsed)
            {
                if (newValue != value)
                {
                    value = activeFloatFieldLastValue = newValue;
                    valueWasChanged = true;
                }
                
            }

            if (active && !recorded)
            { // Gained focus this frame
                activeFloatField = floatFieldID;
                activeFloatFieldString = strValue;
                activeFloatFieldLastValue = value;
            }
            else if (!active && recorded)
            { // Lost focus this frame
                activeFloatField = -1;
                if (!parsed)
                    value = strValue.ForceParseFloat();
            }

            return value;
        }

        /// <summary>
        /// Forces to parse to float by cleaning string if necessary
        /// </summary>
        public static float ForceParseFloat(this string str)
        {
            // try parse
            float value;
            if (float.TryParse(str, out value))
                return value;

            // Clean string if it could not be parsed
            bool recordedDecimalPoint = false;
            List<char> strVal = new List<char>(str);
            for (int cnt = 0; cnt < strVal.Count; cnt++)
            {
                UnicodeCategory type = CharUnicodeInfo.GetUnicodeCategory(str[cnt]);
                if (type != UnicodeCategory.DecimalDigitNumber)
                {
                    strVal.RemoveRange(cnt, strVal.Count - cnt);
                    break;
                }
                else if (str[cnt] == '.')
                {
                    if (recordedDecimalPoint)
                    {
                        strVal.RemoveRange(cnt, strVal.Count - cnt);
                        break;
                    }
                    recordedDecimalPoint = true;
                }
            }

            // Parse again
            if (strVal.Count == 0)
                return 0;
            str = new string(strVal.ToArray());
            if (!float.TryParse(str, out value))
                Debug.LogError("Could not parse " + str);
            return value;
        }

        // Only used for texture paths
        public static string StringField(string value, out bool valueWasChanged)
        {
            valueWasChanged = false;
            string newValue = GUILayout.TextArea(value, HighLogic.Skin.textArea, GUILayout.ExpandWidth(true));
            char[] invalidChars = Path.GetInvalidPathChars();

            // If we have illegal chars in the string
            if (newValue == value || newValue.Any(c => invalidChars.Contains(c)))
            {
                return value;
            }
            else
            {
                valueWasChanged = true;
                return newValue;
            }
        }

        // Int Field
        public static int IntField(int value, out bool valueWasChanged)
        {
            valueWasChanged = false;

            // Get rect and control for this int field for identification
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString()), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.MinWidth(80) });
            int intFieldID = GUIUtility.GetControlID("IntField".GetHashCode(), FocusType.Keyboard, pos) + 1;
            if (intFieldID == 0)
                return value;

            bool recorded = activeIntField == intFieldID;
            bool active = intFieldID == GUIUtility.keyboardControl;

            if (active && recorded && activeIntFieldLastValue != value)
            { // Value has been modified externally
                activeIntFieldLastValue = value;
                activeIntFieldString = value.ToString();
            }

            // Get stored string for the text field if this one is recorded
            string str = recorded ? activeIntFieldString : value.ToString();

            // pass it in the text field
            string strValue = GUI.TextField(pos, str, HighLogic.Skin.textField);

            // Update stored value if this one is recorded
            if (recorded)
                activeIntFieldString = strValue;

            // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
            bool parsed = true;
            if (strValue != value.ToString())
            {
                int newValue;
                parsed = int.TryParse(strValue, out newValue);
                if (parsed)
                {
                    value = activeIntFieldLastValue = newValue;
                    valueWasChanged = true;
                }
            }

            if (active && !recorded)
            { // Gained focus this frame
                activeIntField = intFieldID;
                activeIntFieldString = strValue;
                activeIntFieldLastValue = value;
            }
            else if (!active && recorded)
            { // Lost focus this frame
                activeIntField = -1;
                if (!parsed)
                    value = int.TryParse(strValue, out int forcedValue) ? forcedValue : value; // No ForceParseInt method assumed
            }

            return value;
        }

        // Vector3 field
        public static Vector3 Vector3Field(Vector3 value, out bool valueWasChanged)
        {
            valueWasChanged = false;

            // Get rect and control for this Vector3 field for identification
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString("F3")), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.MinWidth(80) });
            int vector3FieldID = GUIUtility.GetControlID("Vector3Field".GetHashCode(), FocusType.Keyboard, pos) + 1;
            if (vector3FieldID == 0)
                return value;

            bool recorded = activeVector3Field == vector3FieldID;
            bool active = vector3FieldID == GUIUtility.keyboardControl;

            if (active && recorded && activeVector3FieldLastValue != value)
            { // Value has been modified externally
                activeVector3FieldLastValue = value;
                activeVector3FieldString = VectorToString(value);
            }

            // Get stored string for the text field if this one is recorded
            string str = recorded ? activeVector3FieldString : VectorToString(value);

            // pass it in the text field
            string strValue = GUI.TextField(pos, str, HighLogic.Skin.textField);

            // Update stored value if this one is recorded
            if (recorded)
                activeVector3FieldString = strValue;

            // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
            bool parsed = true;
            if (strValue != VectorToString(value))
            {
                Vector3 newValue;
                parsed = TryParseVector3(strValue, out newValue);
                if (parsed && newValue != activeVector3FieldLastValue)
                {
                    value = activeVector3FieldLastValue = newValue;
                    valueWasChanged = true;
                }
            }

            if (active && !recorded)
            { // Gained focus this frame
                activeVector3Field = vector3FieldID;
                activeVector3FieldString = strValue;
                activeVector3FieldLastValue = value;
            }
            else if (!active && recorded)
            { // Lost focus this frame
                activeVector3Field = -1;
                if (!parsed)
                    value = TryParseVector3(strValue, out Vector3 forcedValue) ? forcedValue : value; // No ForceParseVector3 method assumed
            }

            return value;
        }

        // Helper method to parse Vector3 from a string
        private static bool TryParseVector3(string str, out Vector3 result)
        {
            result = Vector3.zero;
            string[] parts = str.Trim('(', ')').Split(',');
            if (parts.Length != 3)
                return false;

            float x, y, z;
            if (float.TryParse(parts[0], out x) && float.TryParse(parts[1], out y) && float.TryParse(parts[2], out z))
            {
                result = new Vector3(x, y, z);
                return true;
            }
            return false;
        }

        // Color field
        public static Color ColorField(Color value, out bool valueWasChanged)
        {
            valueWasChanged = false;

            // Get rect and control for this Color field for identification
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString("F3")), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.MinWidth(80) });
            int colorFieldID = GUIUtility.GetControlID("ColorField".GetHashCode(), FocusType.Keyboard, pos) + 1;
            if (colorFieldID == 0)
                return value;

            bool recorded = activeColorField == colorFieldID;
            bool active = colorFieldID == GUIUtility.keyboardControl;

            if (active && recorded && activeColorFieldLastValue != value)
            { // Value has been modified externally
                activeColorFieldLastValue = value;
                activeColorFieldString = ColorToString(value);
            }

            // Get stored string for the text field if this one is recorded
            string str = recorded ? activeColorFieldString : ColorToString(value);

            // pass it in the text field
            string strValue = GUI.TextField(pos, str, HighLogic.Skin.textField);

            // Update stored value if this one is recorded
            if (recorded)
                activeColorFieldString = strValue;

            // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
            bool parsed = true;
            if (strValue != ColorToString(value))
            {
                Color newValue;
                parsed = TryParseColor(strValue, out newValue);
                if (parsed)
                {
                    value = activeColorFieldLastValue = newValue;
                    valueWasChanged = true;
                    
                }
            }

            if (active && !recorded)
            { // Gained focus this frame
                activeColorField = colorFieldID;
                activeColorFieldString = strValue;
                activeColorFieldLastValue = value;
            }
            else if (!active && recorded)
            { // Lost focus this frame
                activeColorField = -1;
                if (!parsed)
                    value = TryParseColor(strValue, out Color forcedValue) ? forcedValue : value; // No ForceParseColor method assumed
            }

            return value;
        }

        public static bool BoolField(bool value, out bool valueWasChanged)
        {
            valueWasChanged = false;

            // Get rect and control for this bool field for identification
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString()), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.MinWidth(40) });
            int boolFieldID = GUIUtility.GetControlID("BoolField".GetHashCode(), FocusType.Keyboard, pos) + 1;
            if (boolFieldID == 0)
                return value;

            bool recorded = activeBoolField == boolFieldID;
            bool active = boolFieldID == GUIUtility.keyboardControl;

            // Get the current state of the toggle
            bool newValue = GUI.Toggle(pos, value, "", HighLogic.Skin.toggle);

            if (active && !recorded)
            { // Gained focus this frame
                activeBoolField = boolFieldID;
            }
            else if (!active && recorded)
            { // Lost focus this frame
                activeBoolField = -1;
            }

            // If the value changed, update the stored value
            if (newValue != value)
            {
                valueWasChanged = true;
                return newValue;
            }

            return value;
        }

        public static NoiseType EnumField(NoiseType value, out bool valueWasChanged)
        {
            valueWasChanged = false;

            // Get rect and control for this enum field for identification
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString()), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.MinWidth(140) });
            int enumFieldID = GUIUtility.GetControlID("EnumField".GetHashCode(), FocusType.Keyboard, pos) + 1;
            if (enumFieldID == 0)
                return value;

            bool recorded = activeEnumField == enumFieldID;
            bool active = enumFieldID == GUIUtility.keyboardControl;

            if (active && recorded && !activeEnumFieldLastValue.Equals(value))
            { // Value has been modified externally
                activeEnumFieldLastValue = value;
                activeEnumFieldString = value.ToString();
            }

            // Get stored string for the text field if this one is recorded
            string str = recorded ? activeEnumFieldString : value.ToString();

            // pass it in the text field
            string strValue = GUI.TextField(pos, str, HighLogic.Skin.textField);

            // Update stored value if this one is recorded
            if (recorded)
                activeEnumFieldString = strValue;

            // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
            bool parsed = true;
            if (strValue != value.ToString())
            {
                NoiseType newValue;
                parsed = TryParseEnum(strValue, out newValue);
                if (parsed)
                {
                    value = activeEnumFieldLastValue = newValue;
                    valueWasChanged = true;
                }
            }

            if (active && !recorded)
            { // Gained focus this frame
                activeEnumField = enumFieldID;
                activeEnumFieldString = strValue;
                activeEnumFieldLastValue = value;
            }
            else if (!active && recorded)
            { // Lost focus this frame
                activeEnumField = -1;
                if (!parsed)
                    value = TryParseEnum(strValue, out NoiseType forcedValue) ? forcedValue : value;
            }

            return value;
        }

        public static string DropdownField(string value, IList<string> options, string dropdownKey, out bool valueWasChanged)
        {
            valueWasChanged = false;
            InitializeDropdownStyles();

            if (selectedDropdownKey == dropdownKey)
            {
                valueWasChanged = selectedDropdownValue != value;
                value = selectedDropdownValue;
                selectedDropdownKey = null;
            }

            Rect pos = GUILayoutUtility.GetRect(new GUIContent(value), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.MinWidth(160) });
            bool isOpen = openDropdownKey == dropdownKey;
            if (GUI.Button(pos, value, dropdownFieldStyle))
            {
                if (isOpen)
                {
                    CloseDropdown();
                }
                else
                {
                    openDropdownKey = dropdownKey;
                    openDropdownScroll = Vector2.zero;
                }
                isOpen = !isOpen;
            }

            if (isOpen)
            {
                if (Event.current.type != EventType.Layout)
                {
                    openDropdownAnchor = new Rect(GUIUtility.GUIToScreenPoint(pos.position), pos.size);
                }
                openDropdownValue = value;
                openDropdownOptions.Clear();
                openDropdownOptions.AddRange(options);
                openDropdownLastSeenFrame = Time.frameCount;
            }

            return value;
        }

        public static void DrawDropdownPopup(int windowID)
        {
            if (openDropdownKey == null)
            {
                return;
            }
            if (Time.frameCount - openDropdownLastSeenFrame > 1 || openDropdownOptions.Count == 0)
            {
                CloseDropdown();
                return;
            }

            openDropdownPopupRect = GetDropdownPopupRect();

            Event current = Event.current;
            bool clickedOutside = current.type == EventType.MouseDown && !openDropdownPopupRect.Contains(current.mousePosition) && !openDropdownAnchor.Contains(current.mousePosition);
            bool pressedEscape = current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape;
            if (clickedOutside || pressedEscape)
            {
                CloseDropdown();
                if (pressedEscape)
                {
                    current.Use();
                }
                return;
            }

            GUI.Window(windowID, openDropdownPopupRect, DrawDropdownOptions, GUIContent.none, dropdownPopupStyle);
            GUI.BringWindowToFront(windowID);
        }

        public static bool IsMouseOverDropdown()
        {
            if (openDropdownKey == null || Time.frameCount - openDropdownLastSeenFrame > 1)
            {
                return false;
            }
            Vector2 mousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return openDropdownPopupRect.Contains(mousePosition);
        }

        public static void CloseDropdown()
        {
            openDropdownKey = null;
            openDropdownOptions.Clear();
        }

        public static bool CompactButton(string text, float width)
        {
            InitializeDropdownStyles();
            Rect pos = GUILayoutUtility.GetRect(new GUIContent(text), HighLogic.Skin.label, new GUILayoutOption[] { GUILayout.Width(width) });
            return GUI.Button(pos, text, compactButtonStyle);
        }

        private static Rect GetDropdownPopupRect()
        {
            int visibleOptions = Mathf.Min(openDropdownOptions.Count, dropdownMaxVisibleOptions);
            bool needsScrollbar = openDropdownOptions.Count > dropdownMaxVisibleOptions;

            float widestOption = 0;
            foreach (string option in openDropdownOptions)
            {
                widestOption = Mathf.Max(widestOption, dropdownOptionStyle.CalcSize(new GUIContent(option)).x);
            }
            if (needsScrollbar)
            {
                widestOption += HighLogic.Skin.verticalScrollbar.fixedWidth;
            }

            float width = Mathf.Max(openDropdownAnchor.width, widestOption + dropdownPopupStyle.padding.horizontal);
            float height = visibleOptions * dropdownOptionHeight + dropdownPopupStyle.padding.vertical;

            Rect rect = new Rect(openDropdownAnchor.x, openDropdownAnchor.yMax, width, height);
            if (rect.yMax > Screen.height && openDropdownAnchor.y - height >= 0)
            {
                rect.y = openDropdownAnchor.y - height;
            }
            if (rect.xMax > Screen.width)
            {
                rect.x = Mathf.Max(0, Screen.width - width);
            }
            return rect;
        }

        private static void DrawDropdownOptions(int windowID)
        {
            RectOffset padding = dropdownPopupStyle.padding;
            Rect viewRect = new Rect(padding.left, padding.top, openDropdownPopupRect.width - padding.horizontal, openDropdownPopupRect.height - padding.vertical);
            float contentHeight = openDropdownOptions.Count * dropdownOptionHeight;
            bool needsScrollbar = contentHeight > viewRect.height;
            float contentWidth = viewRect.width - (needsScrollbar ? HighLogic.Skin.verticalScrollbar.fixedWidth : 0);

            GUISkin previousSkin = GUI.skin;
            GUI.skin = HighLogic.Skin;
            openDropdownScroll = GUI.BeginScrollView(viewRect, openDropdownScroll, new Rect(0, 0, contentWidth, contentHeight), false, needsScrollbar, GUIStyle.none, HighLogic.Skin.verticalScrollbar);
            GUI.skin = previousSkin;

            for (int i = 0; i < openDropdownOptions.Count; i++)
            {
                string option = openDropdownOptions[i];
                Rect row = new Rect(0, i * dropdownOptionHeight, contentWidth, dropdownOptionHeight);
                GUIStyle style = option == openDropdownValue ? dropdownSelectedOptionStyle : dropdownOptionStyle;
                if (GUI.Button(row, option, style))
                {
                    selectedDropdownKey = openDropdownKey;
                    selectedDropdownValue = option;
                    CloseDropdown();
                    break;
                }
            }

            GUI.EndScrollView();
        }

        private static void InitializeDropdownStyles()
        {
            if (dropdownFieldStyle != null && dropdownBackgroundTexture != null && dropdownHoverTexture != null && dropdownSelectedTexture != null)
            {
                return;
            }

            dropdownBackgroundTexture = CreateSolidTexture(new Color(0.1f, 0.1f, 0.1f, 0.97f));
            dropdownHoverTexture = CreateSolidTexture(new Color(1.0f, 1.0f, 1.0f, 0.15f));
            dropdownSelectedTexture = CreateSolidTexture(new Color(1.0f, 1.0f, 1.0f, 0.07f));

            dropdownFieldStyle = new GUIStyle(HighLogic.Skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            dropdownPopupStyle = new GUIStyle
            {
                padding = new RectOffset(2, 2, 2, 2),
                border = new RectOffset(0, 0, 0, 0)
            };
            dropdownPopupStyle.normal.background = dropdownBackgroundTexture;
            dropdownPopupStyle.onNormal.background = dropdownBackgroundTexture;

            dropdownOptionStyle = new GUIStyle(HighLogic.Skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(6, 6, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                fixedHeight = dropdownOptionHeight
            };
            dropdownOptionStyle.normal.background = null;
            dropdownOptionStyle.hover.background = dropdownHoverTexture;
            dropdownOptionStyle.hover.textColor = Color.white;
            dropdownOptionStyle.active.background = dropdownHoverTexture;
            dropdownOptionStyle.active.textColor = Color.white;

            dropdownSelectedOptionStyle = new GUIStyle(dropdownOptionStyle);
            dropdownSelectedOptionStyle.normal.background = dropdownSelectedTexture;
            dropdownSelectedOptionStyle.normal.textColor = Color.white;

            compactButtonStyle = new GUIStyle(HighLogic.Skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        // Helper method to parse NoiseType enum from a string
        private static bool TryParseEnum(string str, out NoiseType result)
        {
            return Enum.TryParse(str, true, out result) && Enum.IsDefined(typeof(NoiseType), result);
        }


        // Helper method to parse Color from a string
        private static bool TryParseColor(string str, out Color result)
        {
            result = Color.black;
            string[] parts = str.Trim('(', ')').Split(',');
            if (parts.Length != 4)
                return false;

            float r, g, b, a;
            if (float.TryParse(parts[0], out r) && float.TryParse(parts[1], out g) && float.TryParse(parts[2], out b) && float.TryParse(parts[3], out a))
            {
                result = new Color(r, g, b, a);
                return true;
            }
            return false;
        }

        // Helper method to convert Color to string
        private static string ColorToString(Color color)
        {
            return $"{color.r.ToString("F3")}, {color.g.ToString("F3")}, {color.b.ToString("F3")}, {color.a.ToString("F3")}".Replace("(", string.Empty).Replace(")", string.Empty);
        }

        // Helper method to convert Vector3 to string
        private static string VectorToString(Vector3 vector)
        {
            return vector.ToString("F3").Replace("(", string.Empty).Replace(")", string.Empty);
        }
    }
}
