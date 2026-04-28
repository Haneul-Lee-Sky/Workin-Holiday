using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace WorkinHoliday.Editor
{
    public class UIBuilder : EditorWindow
    {
        [MenuItem("WorkinHoliday/Generate Main UI")]
        public static void GenerateUI()
        {
            // 1. Create Canvas
            GameObject canvasGO = new GameObject("MainUI_Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // Balance width & height
            
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // 2. Top HUD
            GameObject hudPanel = new GameObject("Panel_HUD_Top");
            hudPanel.transform.SetParent(canvasGO.transform, false);
            RectTransform hudRect = hudPanel.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 1);
            hudRect.anchorMax = new Vector2(1, 1);
            hudRect.pivot = new Vector2(0.5f, 1);
            hudRect.sizeDelta = new Vector2(0, 300); // Stretch X, 300px height at top
            hudRect.anchoredPosition = Vector2.zero;

            // 2a. Revenue Text (Top Left)
            GameObject revGO = new GameObject("Text_Revenue");
            revGO.transform.SetParent(hudPanel.transform, false);
            Text revText = revGO.AddComponent<Text>();
            revText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            revText.text = "누적 수익 : 33900원\n현재 수익 : 17500원";
            revText.fontSize = 50;
            revText.color = Color.white;
            Outline revOutline = revGO.AddComponent<Outline>();
            revOutline.effectColor = Color.black;
            revOutline.effectDistance = new Vector2(3, -3);

            RectTransform revRect = revGO.GetComponent<RectTransform>();
            revRect.anchorMin = new Vector2(0, 1);
            revRect.anchorMax = new Vector2(0, 1);
            revRect.pivot = new Vector2(0, 1);
            revRect.sizeDelta = new Vector2(600, 200);
            revRect.anchoredPosition = new Vector2(50, -50); // Padding from top left

            // 2a-2. Quest Name Text (수익 아래)
            GameObject questNameGO = new GameObject("Text_QuestName");
            questNameGO.transform.SetParent(hudPanel.transform, false);
            Text questNameText = questNameGO.AddComponent<Text>();
            questNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            questNameText.text = "어서오세요!";
            questNameText.fontSize = 40;
            questNameText.color = Color.yellow;
            Outline questOutline = questNameGO.AddComponent<Outline>();
            questOutline.effectColor = Color.black;
            questOutline.effectDistance = new Vector2(2, -2);

            RectTransform questNameRect = questNameGO.GetComponent<RectTransform>();
            questNameRect.anchorMin = new Vector2(0, 1);
            questNameRect.anchorMax = new Vector2(0, 1);
            questNameRect.pivot = new Vector2(0, 1);
            questNameRect.sizeDelta = new Vector2(600, 60);
            questNameRect.anchoredPosition = new Vector2(50, -220);

            // 2a-3. Quest Progress Text (퀘스트 이름 아래)
            GameObject questProgGO = new GameObject("Text_QuestProgress");
            questProgGO.transform.SetParent(hudPanel.transform, false);
            Text questProgText = questProgGO.AddComponent<Text>();
            questProgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            questProgText.text = "";
            questProgText.fontSize = 32;
            questProgText.color = Color.white;
            Outline questProgOutline = questProgGO.AddComponent<Outline>();
            questProgOutline.effectColor = Color.black;
            questProgOutline.effectDistance = new Vector2(2, -2);

            RectTransform questProgRect = questProgGO.GetComponent<RectTransform>();
            questProgRect.anchorMin = new Vector2(0, 1);
            questProgRect.anchorMax = new Vector2(0, 1);
            questProgRect.pivot = new Vector2(0, 1);
            questProgRect.sizeDelta = new Vector2(800, 50);
            questProgRect.anchoredPosition = new Vector2(50, -280);

            // 2b. Timer Panel (Top Right)
            GameObject timerGO = new GameObject("Panel_Timer");
            timerGO.transform.SetParent(hudPanel.transform, false);
            RectTransform timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(1, 1);
            timerRect.anchorMax = new Vector2(1, 1);
            timerRect.pivot = new Vector2(1, 1);
            timerRect.sizeDelta = new Vector2(500, 100);
            timerRect.anchoredPosition = new Vector2(-50, -50); // Padding from top right
            
            // Timer Clock Icon Placeholder
            GameObject clockGO = new GameObject("Image_ClockIcon");
            clockGO.transform.SetParent(timerGO.transform, false);
            RectTransform clockRect = clockGO.AddComponent<RectTransform>();
            clockRect.anchorMin = new Vector2(0, 0.5f);
            clockRect.anchorMax = new Vector2(0, 0.5f);
            clockRect.pivot = new Vector2(0, 0.5f);
            clockRect.sizeDelta = new Vector2(100, 100);
            clockRect.anchoredPosition = new Vector2(0, 0);
            Image clockImg = clockGO.AddComponent<Image>();
            clockImg.color = Color.red;

            // Timer Bar Fake Slider
            GameObject timerBarBgGO = new GameObject("Image_TimerBg");
            timerBarBgGO.transform.SetParent(timerGO.transform, false);
            RectTransform tbBgRect = timerBarBgGO.AddComponent<RectTransform>();
            tbBgRect.anchorMin = new Vector2(0, 0.5f);
            tbBgRect.anchorMax = new Vector2(1, 0.5f);
            tbBgRect.pivot = new Vector2(0, 0.5f);
            tbBgRect.sizeDelta = new Vector2(-120, 40); // Leave space for clock
            tbBgRect.anchoredPosition = new Vector2(120, 0);
            Image tbBgImg = timerBarBgGO.AddComponent<Image>();
            tbBgImg.color = Color.black;

            GameObject timerBarFillGO = new GameObject("Image_TimerFill");
            timerBarFillGO.transform.SetParent(timerBarBgGO.transform, false);
            RectTransform tbFillRect = timerBarFillGO.AddComponent<RectTransform>();
            tbFillRect.anchorMin = new Vector2(0, 0);
            tbFillRect.anchorMax = new Vector2(0.8f, 1); // 80% full
            tbFillRect.pivot = new Vector2(0, 0.5f);
            tbFillRect.sizeDelta = new Vector2(0, -10); // Padding inside
            tbFillRect.anchoredPosition = new Vector2(5, 0);
            Image tbFillImg = timerBarFillGO.AddComponent<Image>();
            tbFillImg.color = Color.green;

            // 3. Maker Center
            GameObject makerGO = new GameObject("Panel_Maker_Center");
            makerGO.transform.SetParent(canvasGO.transform, false);
            RectTransform makerRect = makerGO.AddComponent<RectTransform>();
            makerRect.anchorMin = new Vector2(0.5f, 0.5f);
            makerRect.anchorMax = new Vector2(0.5f, 0.5f);
            makerRect.pivot = new Vector2(0.5f, 0.5f);
            makerRect.sizeDelta = new Vector2(600, 400);
            makerRect.anchoredPosition = new Vector2(0, 600); // Moved up visually
            Image makerBg = makerGO.AddComponent<Image>();
            makerBg.color = Color.white;
            Outline makerOutline = makerGO.AddComponent<Outline>();
            makerOutline.effectColor = new Color(0.3f, 0.3f, 0.3f);
            makerOutline.effectDistance = new Vector2(10, -10);

            // Completed Ice Preview
            GameObject iceGO = new GameObject("Image_IcePreview");
            iceGO.transform.SetParent(makerGO.transform, false);
            RectTransform iceRect = iceGO.AddComponent<RectTransform>();
            iceRect.anchorMin = new Vector2(0.5f, 0.5f);
            iceRect.anchorMax = new Vector2(0.5f, 0.5f);
            iceRect.pivot = new Vector2(0.5f, 0.5f);
            iceRect.sizeDelta = new Vector2(300, 300);
            Image iceImg = iceGO.AddComponent<Image>();
            iceImg.color = new Color(0.8f, 0.9f, 1f); // Ice color

            // 4. Toppings Bottom
            GameObject toppingGO = new GameObject("Panel_Toppings_Bottom");
            toppingGO.transform.SetParent(canvasGO.transform, false);
            RectTransform toppingRect = toppingGO.AddComponent<RectTransform>();
            // Anchor to Bottom Stretch
            toppingRect.anchorMin = new Vector2(0, 0);
            toppingRect.anchorMax = new Vector2(1, 0);
            toppingRect.pivot = new Vector2(0.5f, 0);
            toppingRect.sizeDelta = new Vector2(0, 450); // Height 450
            toppingRect.anchoredPosition = new Vector2(0, 100); // 100px padding from bottom
            
            HorizontalLayoutGroup topLayout = toppingGO.AddComponent<HorizontalLayoutGroup>();
            topLayout.childControlHeight = true;
            topLayout.childControlWidth = true;
            topLayout.childForceExpandHeight = true;
            topLayout.childForceExpandWidth = true;
            topLayout.padding = new RectOffset(100, 100, 0, 0);
            topLayout.spacing = 50;

            CreateToppingGroup(toppingGO.transform, "Group_RedBean", "←");
            CreateToppingGroup(toppingGO.transform, "Group_Milk", "↓");
            CreateToppingGroup(toppingGO.transform, "Group_Fruit", "→");

            // Finalize
            Selection.activeGameObject = canvasGO;
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Main UI");
            Debug.Log("Main UI Framework generated successfully with updated feedback!");
        }

        private static void CreatePlaceholderImage(Transform parent, string name)
        {
            GameObject btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            Image img = btnGO.AddComponent<Image>();
            img.color = Color.white;
            Outline outline = btnGO.AddComponent<Outline>();
            outline.effectColor = Color.gray;
            outline.effectDistance = new Vector2(3, -3);
            btnGO.AddComponent<Button>();
        }

        private static void CreateToppingGroup(Transform parent, string name, string arrowTextStr)
        {
            GameObject groupGO = new GameObject(name);
            groupGO.transform.SetParent(parent, false);
            groupGO.AddComponent<RectTransform>(); 
            
            VerticalLayoutGroup vlayout = groupGO.AddComponent<VerticalLayoutGroup>();
            vlayout.childControlHeight = true;
            vlayout.childControlWidth = true;
            vlayout.spacing = 15;
            
            GameObject iconGO = new GameObject("Image_Icon");
            iconGO.transform.SetParent(groupGO.transform, false);
            Image img = iconGO.AddComponent<Image>();
            img.color = new Color(0.8f, 0.4f, 0.4f); // placeholder color
            LayoutElement leIcon = iconGO.AddComponent<LayoutElement>();
            leIcon.preferredHeight = 250; 
            leIcon.layoutPriority = 1;
            
            GameObject btnGO = new GameObject("Btn_InputMap");
            btnGO.transform.SetParent(groupGO.transform, false);
            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = Color.white;
            Outline outline = btnGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            Button buttonComponent = btnGO.AddComponent<Button>();
            btnGO.AddComponent<ButtonPressFeedback>();
            LayoutElement leBtn = btnGO.AddComponent<LayoutElement>();
            leBtn.preferredHeight = 150;
            leBtn.layoutPriority = 1;

            GameObject textGO = new GameObject("Text_Arrow");
            textGO.transform.SetParent(btnGO.transform, false);
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            Text text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = arrowTextStr;
            text.fontSize = 80;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
        }
    }
}
