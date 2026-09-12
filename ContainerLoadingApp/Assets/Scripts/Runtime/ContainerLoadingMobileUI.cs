using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class ContainerLoadingApp
{
    Canvas mobileCanvas;
    RectTransform mobileRoot;
    ScrollRect mobileScrollRect;
    Font mobileFont;
    Sprite mobileSprite;

    static readonly Color UiBackground = new(0.953f, 0.965f, 0.976f, 1);
    static readonly Color UiSurface = Color.white;
    static readonly Color UiPrimary = new(0.059f, 0.165f, 0.263f, 1);
    static readonly Color UiAccent = new(0.145f, 0.388f, 0.922f, 1);
    static readonly Color UiPrimarySoft = new(0.914f, 0.941f, 0.976f, 1);
    static readonly Color UiText = new(0.118f, 0.161f, 0.231f, 1);
    static readonly Color UiMuted = new(0.278f, 0.333f, 0.412f, 1);
    static readonly Color UiSuccess = new(0.082f, 0.502f, 0.239f, 1);
    static readonly Color UiWarning = new(0.706f, 0.325f, 0.035f, 1);
    static readonly Color UiDanger = new(0.725f, 0.11f, 0.11f, 1);
    static readonly Color UiBorder = new(0.886f, 0.91f, 0.941f, 1);

    void SetupMobileUI()
    {
        GameObject canvasObject = null;
        try
        {
            mobileFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            mobileSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
            canvasObject = new GameObject("MobileAppCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            mobileCanvas = canvasObject.GetComponent<Canvas>();
            mobileCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mobileCanvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            mobileRoot = canvasObject.GetComponent<RectTransform>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule), typeof(BaseInput));
                DontDestroyOnLoad(eventSystem);
            }
            DontDestroyOnLoad(canvasObject);
            RefreshMobileUI();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (canvasObject) Destroy(canvasObject);
            mobileCanvas = null;
            mobileRoot = null;
        }
    }

    void RefreshMobileUI(bool preserveScroll = false)
    {
        if (!mobileRoot) return;
        var previousScrollOffset = mobileScrollRect && mobileScrollRect.content ? mobileScrollRect.content.anchoredPosition.y : 0f;
        var anchorName = preserveScroll && screen == ScreenMode.Editor ? "PositionController" : null;
        var previousAnchorScreenY = ScreenYOfChild(mobileScrollRect, anchorName);
        mobileScrollRect = null;
        for (var i = mobileRoot.childCount - 1; i >= 0; i--) Destroy(mobileRoot.GetChild(i).gameObject);
        if (screen == ScreenMode.Editor) BuildMobileEditor();
        else if (screen == ScreenMode.Home) BuildMobileHome();
        else if (screen == ScreenMode.Container) BuildMobileContainer();
        else if (screen == ScreenMode.Cargo) BuildMobileCargo();
        else if (screen == ScreenMode.Detail) BuildMobileDetail();
        else BuildMobileSettings();
        Canvas.ForceUpdateCanvases();
        foreach (var fitter in mobileRoot.GetComponentsInChildren<ContentSizeFitter>())
            LayoutRebuilder.ForceRebuildLayoutImmediate(fitter.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
        if (preserveScroll && mobileScrollRect)
        {
            SetMobileScrollOffset(mobileScrollRect, previousScrollOffset);
            StartCoroutine(RestoreMobileScrollNextFrame(mobileScrollRect, previousScrollOffset, anchorName, previousAnchorScreenY));
        }
    }

    float? ScreenYOfChild(ScrollRect target, string childName)
    {
        if (!target || string.IsNullOrEmpty(childName)) return null;
        var child = target.content.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x => x.name == childName);
        return child ? RectTransformUtility.WorldToScreenPoint(null, child.position).y : null;
    }

    void SetMobileScrollOffset(ScrollRect target, float offset)
    {
        if (!target || !target.content || !target.viewport) return;
        var maximum = Mathf.Max(0, target.content.rect.height - target.viewport.rect.height);
        var position = target.content.anchoredPosition; position.y = Mathf.Clamp(offset, 0, maximum); target.content.anchoredPosition = position;
    }

    IEnumerator RestoreMobileScrollNextFrame(ScrollRect target, float offset, string anchorName, float? anchorScreenY)
    {
        yield return null;
        if (!target) yield break;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(target.content);
        SetMobileScrollOffset(target, offset);
        Canvas.ForceUpdateCanvases();
        var currentAnchorScreenY = ScreenYOfChild(target, anchorName);
        if (anchorScreenY.HasValue && currentAnchorScreenY.HasValue)
            SetMobileScrollOffset(target, target.content.anchoredPosition.y + anchorScreenY.Value - currentAnchorScreenY.Value);
    }

    RectTransform Object(string name, Transform parent)
    {
        var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        value.SetParent(parent, false);
        return value;
    }

    void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }

    void ApplySafeArea(RectTransform rect)
    {
        var safe = Screen.safeArea;
        rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    Image Surface(Transform parent, string name, Color color)
    {
        var rect = Object(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = mobileSprite; image.color = color;
        return image;
    }

    Text MobileText(Transform parent, string value, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var text = Object("Text", parent).gameObject.AddComponent<Text>();
        text.font = mobileFont; text.text = value; text.fontSize = size; text.color = color; text.fontStyle = style;
        text.alignment = alignment; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var explicitLines = Mathf.Max(1, (value ?? "").Count(c => c == '\n') + 1);
        var wrappedLines = Mathf.Max(explicitLines, Mathf.CeilToInt((value?.Length ?? 0) / 48f));
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(46, size * 1.45f * wrappedLines);
        return text;
    }

    Button MobileButton(Transform parent, string value, Action action, bool primary = false, bool danger = false, float height = 82)
    {
        var image = Surface(parent, value + "Button", danger ? UiDanger : primary ? UiAccent : UiPrimarySoft);
        var buttonComponent = image.gameObject.AddComponent<Button>();
        buttonComponent.targetGraphic = image;
        buttonComponent.interactable = !busy;
        var colors = buttonComponent.colors; colors.highlightedColor = new Color(.83f, .90f, .93f); colors.pressedColor = new Color(.72f, .84f, .88f); buttonComponent.colors = colors;
        buttonComponent.onClick.AddListener(() => action());
        image.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(88, height);
        var text = MobileText(image.transform, value, 27, primary || danger ? Color.white : UiPrimary, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return buttonComponent;
    }

    RectTransform Vertical(Transform parent, string name, Color? background = null, int spacing = 18, int padding = 0)
    {
        var rect = background.HasValue ? Surface(parent, name, background.Value).rectTransform : Object(name, parent);
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing; layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        return rect;
    }

    RectTransform Horizontal(Transform parent, string name, int spacing = 14)
    {
        var rect = Object(name, parent);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 88;
        return rect;
    }

    void SectionHeader(Transform parent, string titleText, string subtitle = null)
    {
        MobileText(parent, titleText, 27, UiPrimary, FontStyle.Bold);
        if (!string.IsNullOrWhiteSpace(subtitle)) MobileText(parent, subtitle, 22, UiMuted);
    }

    void StatusBanner(Transform parent, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message == "Sẵn sàng") return;
        var danger = message.StartsWith("✕") || message.StartsWith("Không") || message.Contains("lỗi") || message.Contains("ngoài");
        var success = message.StartsWith("✓") || message.StartsWith("Đã");
        var color = danger ? UiDanger : success ? UiSuccess : UiPrimary;
        var banner = Vertical(parent, "StatusBanner", Color.Lerp(color, Color.white, .9f), 4, 18);
        MobileText(banner, message, 23, color, FontStyle.Bold);
    }

    void StatCard(Transform parent, string value, string caption)
    {
        var card = Vertical(parent, "StatCard", UiSurface, 0, 16);
        MobileText(card, value, 31, UiPrimary, FontStyle.Bold, TextAnchor.MiddleCenter);
        MobileText(card, caption, 20, UiMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
    }

    void ProgressBar(Transform parent, float value)
    {
        var track = Surface(parent, "ProgressTrack", UiBorder); track.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
        var fill = Surface(track.transform, "ProgressFill", value >= .999f ? UiSuccess : UiAccent).rectTransform;
        Stretch(fill, Vector2.zero, new Vector2(Mathf.Clamp01(value), 1), Vector2.zero, Vector2.zero);
    }

    void NumberStepper(Transform parent, string caption, int axis, int value)
    {
        MobileText(parent, caption, 22, UiMuted, FontStyle.Bold);
        var row = Horizontal(parent, caption + "Stepper", 10);
        MobileButton(row, "−", () => AdjustPreview(axis, -1));
        MobileText(row, (value + 1).ToString(), 31, UiText, FontStyle.Bold, TextAnchor.MiddleCenter);
        MobileButton(row, "+", () => AdjustPreview(axis, 1));
    }

    InputField MobileInput(Transform parent, string caption, string value, Action<string> changed, string placeholder = "", InputField.ContentType contentType = InputField.ContentType.Standard)
    {
        if (!string.IsNullOrEmpty(caption)) MobileText(parent, caption, 24, UiMuted, FontStyle.Bold);
        var image = Surface(parent, caption + "Input", UiSurface);
        image.gameObject.AddComponent<LayoutElement>().preferredHeight = 82;
        var field = image.gameObject.AddComponent<InputField>();
        var text = MobileText(image.transform, value, 30, UiText);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(22, 8), new Vector2(-22, -8));
        var hint = MobileText(image.transform, placeholder, 28, new Color(.55f, .59f, .64f));
        Stretch(hint.rectTransform, Vector2.zero, Vector2.one, new Vector2(22, 8), new Vector2(-22, -8));
        field.textComponent = text; field.placeholder = hint; field.text = value;
        field.contentType = contentType;
        field.characterLimit = PlanValidation.MaxTextLength;
        field.onValueChanged.AddListener(v => { changed(v); if (screen == ScreenMode.Container || screen == ScreenMode.Cargo) formDirty = true; });
        return field;
    }

    InputField MobileMultilineInput(Transform parent, string caption, string value, Action<string> changed, int height = 180)
    {
        var field = MobileInput(parent, caption, value, changed, "Nhập ghi chú đơn hàng...");
        field.lineType = InputField.LineType.MultiLineNewline;
        field.characterLimit = PlanValidation.MaxNotesLength;
        field.GetComponent<LayoutElement>().preferredHeight = height;
        field.textComponent.alignment = TextAnchor.UpperLeft;
        return field;
    }

    Button MobileColorSwatch(Transform parent, Color color, Action selected)
    {
        var image = Surface(parent, "ColorSwatch", color);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = color; colors.highlightedColor = Color.Lerp(color, Color.white, .25f); colors.pressedColor = Color.Lerp(color, Color.black, .2f); button.colors = colors;
        button.onClick.AddListener(() => selected());
        image.gameObject.AddComponent<LayoutElement>().preferredHeight = 74;
        return button;
    }

    RectTransform BuildPage(string titleText, string subtitle, bool plus, Action back = null)
    {
        var backdrop = Surface(mobileRoot, "AppBackground", UiBackground).rectTransform;
        Stretch(backdrop, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var background = Object("SafeArea", backdrop); ApplySafeArea(background);
        var header = Surface(background, "Header", UiSurface).rectTransform;
        Stretch(header, new Vector2(0, 1), Vector2.one, new Vector2(0, -148), Vector2.zero);
        if (back != null)
        {
            var backButton = MobileButton(header, "Quay lại", back, false, false, 86);
            var rect = backButton.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.pivot = new Vector2(0, .5f); rect.anchoredPosition = new Vector2(24, -12); rect.sizeDelta = new Vector2(160, 88);
        }
        var left = back == null ? 38 : 202;
        var title = MobileText(header, titleText, 38, UiText, FontStyle.Bold);
        Stretch(title.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(left, 42), new Vector2(plus ? -250 : -34, -12));
        var sub = MobileText(header, subtitle, 22, UiMuted);
        Stretch(sub.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(left, 8), new Vector2(plus ? -250 : -34, -76));
        if (plus)
        {
            var add = MobileButton(header, "Tạo mới", () => { BeginNewContainer(); RefreshMobileUI(); }, true, false, 88);
            var rect = add.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(1, .5f); rect.anchoredPosition = new Vector2(-24, -12); rect.sizeDelta = new Vector2(210, 88);
        }
        var body = MobileScroll(background, 148, 112);
        BuildBottomNavigation(background);
        return body;
    }

    RectTransform MobileScroll(Transform parent, float top, float bottom)
    {
        var rootScroll = Object("ScrollView", parent);
        Stretch(rootScroll, Vector2.zero, Vector2.one, new Vector2(0, bottom), new Vector2(0, -top));
        var scroll = rootScroll.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        mobileScrollRect = scroll;
        var viewport = Object("Viewport", rootScroll); Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var viewportImage = viewport.gameObject.AddComponent<Image>(); viewportImage.sprite = mobileSprite; viewportImage.color = new Color(1, 1, 1, 0); viewportImage.raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Vertical(viewport, "Content", null, 22, 32);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        scroll.viewport = viewport; scroll.content = content;
        scroll.verticalNormalizedPosition = 1;
        return content;
    }

    void BuildBottomNavigation(Transform parent)
    {
        var nav = Surface(parent, "BottomNavigation", UiSurface).rectTransform;
        Stretch(nav, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 112));
        var row = Horizontal(nav, "Navigation", 12); Stretch(row, Vector2.zero, Vector2.one, new Vector2(24, 10), new Vector2(-24, -10));
        MobileButton(row, "Trang chủ", OpenHome, screen == ScreenMode.Home);
        MobileButton(row, "Cài đặt", () => { screen = ScreenMode.Settings; RefreshMobileUI(); }, screen == ScreenMode.Settings);
    }

    void BuildMobileHome()
    {
        if (BuildMobileShipmentHome()) return;
        var content = BuildPage("Container Loading", "Điều phối phương án xếp hàng", true);
        StatusBanner(content, status);
        SectionHeader(content, "Phương án gần đây", "Tìm và tiếp tục công việc đang thực hiện");
        var searchRow = Horizontal(content, "Search", 12);
        var searchField = MobileInput(searchRow, "", search, v => search = v, "Tìm theo mã hoặc loại container...");
        searchField.onEndEdit.AddListener(_ => RefreshMobileUI());
        MobileButton(searchRow, "Tìm", () => RefreshMobileUI(), true);
        if (!string.IsNullOrWhiteSpace(search)) MobileButton(searchRow, "Xóa lọc", () => { search = ""; RefreshMobileUI(); });
        var files = PlanPersistence.ListFiles();
        var loaded = files.Select(path => new { path, data = PlanPersistence.Load(path) }).ToList();
        var corruptCount = loaded.Count(x => x.data == null);
        if (corruptCount > 0) StatusBanner(content, $"Không thể đọc {corruptCount} tệp dữ liệu. Các phương án hợp lệ vẫn an toàn.");
        var validPlans=loaded.Where(x=>x.data!=null).ToList();
        if(validPlans.Count>0){var dashboard=Horizontal(content,"Dashboard",10);StatCard(dashboard,validPlans.Count.ToString(),"Tổng PA");StatCard(dashboard,validPlans.Count(x=>PlanIntelligence.RemainingQuantity(x.data)==0).ToString(),"Hoàn tất");StatCard(dashboard,validPlans.Count(x=>PlanIntelligence.RemainingQuantity(x.data)>0).ToString(),"Đang làm");}
        var sortRow=Horizontal(content,"SortPlans",10);MobileButton(sortRow,sortNewest?"Sắp xếp: Mới nhất":"Sắp xếp: Tên A–Z",()=>{sortNewest=!sortNewest;RefreshMobileUI();});
        var filtered=loaded.Where(x => x.data != null && PlanPersistence.MatchesSearch(x.data, search));
        var entries = (sortNewest?filtered.OrderByDescending(x=>File.GetLastWriteTimeUtc(x.path)):filtered.OrderBy(x=>x.data.name)).ToList();
        if (entries.Count == 0)
        {
            var empty = Vertical(content, "EmptyState", UiSurface, 12, 34); empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 360;
            MobileText(empty, string.IsNullOrWhiteSpace(search) ? "Chưa có phương án" : "Không tìm thấy phương án", 34, UiText, FontStyle.Bold, TextAnchor.MiddleCenter);
            MobileText(empty, string.IsNullOrWhiteSpace(search) ? "Tạo phương án đầu tiên để bắt đầu." : "Hãy thử tên, mã đơn hàng hoặc container khác.", 24, UiMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (string.IsNullOrWhiteSpace(search)) MobileButton(empty, "Tạo phương án", () => { BeginNewContainer(); RefreshMobileUI(); }, true);
            return;
        }
        foreach (var item in entries)
        {
            var data = item.data; var used = UsedVolume(data); var capacity = Mathf.Max(1, data.container.length * data.container.width * data.container.height); var percent = Mathf.Clamp(Mathf.RoundToInt(used * 100f / capacity), 0, 100);
            var cardRoot = Vertical(content, "PlanCard", UiSurface, 8, 24); cardRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = string.IsNullOrWhiteSpace(data.orderReference) ? 300 : 342;
            var top = Horizontal(cardRoot, "CardHeader");
            var titles = Vertical(top, "Titles", null, 2); MobileText(titles, data.name, 34, UiText, FontStyle.Bold); MobileText(titles, data.container.name, 24, UiMuted);
            var remaining=PlanIntelligence.RemainingQuantity(data);var total=PlanIntelligence.TotalQuantity(data);var state = total>0&&remaining==0 ? "Hoàn tất" : data.placedCargo.Count==0 ? "Chưa bắt đầu" : "Đang xếp"; MobileText(top, state, 21, remaining==0&&total>0 ? UiSuccess : UiPrimary, FontStyle.Bold, TextAnchor.MiddleRight);
            MobileText(cardRoot, $"{data.container.length} × {data.container.width} × {data.container.height}   •   Đã xếp {data.placedCargo.Count}/{total}   •   {percent}%", 27, UiText);
            if (!string.IsNullOrWhiteSpace(data.orderReference)) MobileText(cardRoot, "Đơn hàng: " + data.orderReference, 23, UiMuted);
            MobileText(cardRoot, "Cập nhật " + File.GetLastWriteTime(item.path).ToString("dd/MM/yyyy HH:mm"), 22, UiMuted);
            var actions = Horizontal(cardRoot, "Actions");
            MobileButton(actions, "Mở", () => OpenPlan(item.path), true);
            MobileButton(actions, "Tùy chọn", () => ShowPlanActions(item.path, data));
        }
    }

    void BuildMobileContainer()
    {
        var content = BuildPage(editingContainer ? "Chỉnh sửa phương án" : "Tạo phương án", "Thông tin đơn hàng và container", false, RequestBackNavigation);
        StatusBanner(content, status);
        SectionHeader(content, "Container", "Chọn kích thước nhanh hoặc nhập tùy chỉnh");
        var presets = Horizontal(content, "Presets");
        MobileButton(presets, "20FT", () => { Preset("20FT", 6, 3, 3); formDirty = true; RefreshMobileUI(); });
        MobileButton(presets, "40FT", () => { Preset("40FT", 12, 3, 3); formDirty = true; RefreshMobileUI(); });
        MobileButton(presets, "40FT HC", () => { Preset("40FT HC", 12, 3, 4); formDirty = true; RefreshMobileUI(); });
        var container = Vertical(content, "ContainerForm", UiSurface, 10, 24);
        MobileInput(container, "Tên container", containerName, v => containerName = v);
        MobileInput(container, "Dài (ô)", containerLength, v => containerLength = v, "", InputField.ContentType.IntegerNumber);
        MobileInput(container, "Rộng (ô)", containerWidth, v => containerWidth = v, "", InputField.ContentType.IntegerNumber);
        MobileInput(container, "Cao (ô)", containerHeight, v => containerHeight = v, "", InputField.ContentType.IntegerNumber);
        SectionHeader(content, "Thông tin phương án");
        var form = Vertical(content, "PlanForm", UiSurface, 10, 24);
        MobileInput(form, "Tên phương án", containerCode, v => containerCode = v);
        MobileInput(form, "Mã đơn hàng / tham chiếu", orderReference, v => orderReference = v);
        MobileInput(form, "Khách hàng", customerName, v => customerName = v);
        MobileInput(form, "Số container", containerNumber, v => containerNumber = v);
        MobileInput(form, "Số niêm phong", sealNumber, v => sealNumber = v);
        MobileInput(form, "Điểm đến", destination, v => destination = v);
        MobileInput(form, "Ngày đóng hàng", loadingDate, v => loadingDate = v, "VD: 12/09/2026");
        MobileMultilineInput(form, "Ghi chú", orderNotes, v => orderNotes = v);
        MobileButton(content, editingContainer ? "LƯU THAY ĐỔI" : "TẠO PHƯƠNG ÁN", () => { SaveContainer(); RefreshMobileUI(); }, true);
    }

    void BuildMobileDetail()
    {
        if (BuildMobileShipmentDetail()) return;
        var content = BuildPage(plan.name, plan.container.name + $"  •  {plan.container.length} × {plan.container.width} × {plan.container.height}", false, OpenHome);
        StatusBanner(content, status);
        var used = UsedVolume(plan); var capacity = Mathf.Max(1, plan.container.length * plan.container.width * plan.container.height); var percent = Mathf.Clamp(Mathf.RoundToInt(used * 100f / capacity), 0, 100);
        SectionHeader(content, "Tổng quan");
        var total=PlanIntelligence.TotalQuantity(plan);var remaining=PlanIntelligence.RemainingQuantity(plan);
        var stats = Horizontal(content, "OverviewStats", 10); StatCard(stats, plan.placedCargo.Count+"/"+total, "Đã xếp"); StatCard(stats, remaining.ToString(), "Còn lại"); StatCard(stats, percent + "%", "Sử dụng");
        var overview = Vertical(content, "Capacity", UiSurface, 8, 24); MobileText(overview, $"{used} / {capacity} ô thể tích", 25, UiText, FontStyle.Bold); ProgressBar(overview, used / (float)capacity);
        MobileText(overview,$"Tổng trọng lượng đã xếp: {PlanIntelligence.TotalWeight(plan):0.##} kg",22,UiMuted);
        var completion=remaining==0&&total>0?"Hoàn tất":"Chưa hoàn tất";MobileText(overview,completion,23,remaining==0&&total>0?UiSuccess:UiWarning,FontStyle.Bold);
        SectionHeader(content, "Sơ đồ xếp hàng");
        if (plan.cargoTypes.Count == 0) StatusBanner(content, "Chưa có loại hàng. Hãy khai báo loại hàng trước khi mở trình xếp.");
        var editor = MobileButton(content, "MỞ TRÌNH XẾP HÀNG 3D", () => { screen = ScreenMode.Editor; suppressEditorPointer = true; status = "Chọn loại hàng để bắt đầu xếp."; ResetCamera(); RefreshMobileUI(); }, true);
        editor.interactable = plan.cargoTypes.Count > 0;
        SectionHeader(content, "Hàng hóa");
        MobileButton(content, "Quản lý loại hàng", () => { screen = ScreenMode.Cargo; ClearCargo(); RefreshMobileUI(); });
        SectionHeader(content, "Báo cáo");
        var reportActions=Horizontal(content,"HealthActions");MobileButton(reportActions,"KIỂM TRA PHƯƠNG ÁN",ShowPlanHealth,true);MobileButton(reportActions,"Xuất báo cáo PDF",ShowPdfExportDialog);
        var pdf = Horizontal(content, "PdfActions"); MobileButton(pdf, "Mở PDF", OpenPdf); MobileButton(pdf, "Chia sẻ", SharePdf);
        SectionHeader(content, "Thông tin đơn hàng");
        var info = Vertical(content, "OrderInfo", UiSurface, 6, 24); MobileText(info, "Mã đơn hàng: " + (string.IsNullOrWhiteSpace(plan.orderReference) ? "Chưa có" : plan.orderReference), 23, UiText); MobileText(info, "Khách hàng: " + (string.IsNullOrWhiteSpace(plan.customerName) ? "Chưa có" : plan.customerName), 23, UiText);if(!string.IsNullOrWhiteSpace(plan.containerNumber))MobileText(info,"Số container: "+plan.containerNumber,23,UiText);if(!string.IsNullOrWhiteSpace(plan.sealNumber))MobileText(info,"Niêm phong: "+plan.sealNumber,23,UiText);if(!string.IsNullOrWhiteSpace(plan.destination))MobileText(info,"Điểm đến: "+plan.destination,23,UiText);if(!string.IsNullOrWhiteSpace(plan.loadingDate))MobileText(info,"Ngày đóng hàng: "+plan.loadingDate,23,UiText); if(!string.IsNullOrWhiteSpace(plan.orderNotes))MobileText(info,"Ghi chú: "+plan.orderNotes,22,UiMuted); MobileText(info,"Cập nhật: "+FormatUpdatedAt(plan.updatedAt),21,UiMuted);
        var actions = Horizontal(content, "PlanActions"); MobileButton(actions, "Chỉnh sửa", () => { BeginEditContainer(); RefreshMobileUI(); }); MobileButton(actions, "Sao lưu JSON", ExportPlanData); MobileButton(actions, "Nhập dữ liệu", BeginImportPlan);
        var csvActions=Horizontal(content,"CsvActions");MobileButton(csvActions,"Nhập hàng CSV",BeginImportCargoCsv);MobileButton(csvActions,"Xuất hàng CSV",ExportCargoCsv);
    }

    void BuildMobileCargo()
    {
        var content = BuildPage("Quản lý loại hàng", plan.name, false, RequestBackNavigation);
        StatusBanner(content, status);
        MobileButton(content, "+ THÊM LOẠI HÀNG", () => { ClearCargo(); cargoFormVisible = true; RefreshMobileUI(); }, true);
        SectionHeader(content, "Danh mục hàng hóa");
        var cargoSearchRow=Horizontal(content,"CargoSearch",10);var cargoSearchField=MobileInput(cargoSearchRow,"",cargoSearch,v=>cargoSearch=v,"Tìm mã hoặc tên hàng...");cargoSearchField.onEndEdit.AddListener(_=>RefreshMobileUI());MobileButton(cargoSearchRow,"Tìm",()=>RefreshMobileUI(),true);
        var cargoFilters=Horizontal(content,"CargoFilters",8);MobileButton(cargoFilters,"Tất cả",()=>{cargoFilter=CargoFilter.All;RefreshMobileUI();},cargoFilter==CargoFilter.All);MobileButton(cargoFilters,"Còn lại",()=>{cargoFilter=CargoFilter.Remaining;RefreshMobileUI();},cargoFilter==CargoFilter.Remaining);MobileButton(cargoFilters,"Đã đủ",()=>{cargoFilter=CargoFilter.Complete;RefreshMobileUI();},cargoFilter==CargoFilter.Complete);
        if (plan.cargoTypes.Count == 0)
        {
            var empty = Vertical(content, "CargoEmpty", UiSurface, 8, 28); MobileText(empty, "Chưa có loại hàng", 31, UiText, FontStyle.Bold, TextAnchor.MiddleCenter); MobileText(empty, "Thêm loại hàng để bắt đầu bố trí trong container.", 23, UiMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
        }
        var visibleCargo=plan.cargoTypes.Where(type=>(string.IsNullOrWhiteSpace(cargoSearch)||(type.code??"").IndexOf(cargoSearch,StringComparison.OrdinalIgnoreCase)>=0||(type.name??"").IndexOf(cargoSearch,StringComparison.OrdinalIgnoreCase)>=0)&&CargoMatchesFilter(type)).ToArray();
        if(plan.cargoTypes.Count>0&&visibleCargo.Length==0)StatusBanner(content,"Không có loại hàng khớp bộ lọc.");
        foreach (var type in visibleCargo)
        {
            var item = Vertical(content, "CargoCard", UiSurface, 7, 22); item.gameObject.AddComponent<LayoutElement>().preferredHeight = 310;
            var heading = Horizontal(item, "CargoHeading", 12); var cargoSwatch = Surface(heading, "CargoColor", type.color); cargoSwatch.gameObject.AddComponent<LayoutElement>().preferredWidth = 28; MobileText(heading, type.code + "  ·  " + type.name, 30, UiText, FontStyle.Bold);
            var placed=shipment.containers.Sum(c=>c.placedCargo.Count(x=>x.cargoTypeId==type.id));MobileText(item, $"{type.length} × {type.width} × {type.height} ô   ·   Toàn chuyến {placed}/{type.quantity}   ·   Còn {ShipmentIntelligence.Remaining(shipment,type)}", 23, UiMuted);
            MobileText(item,$"{type.weightPerUnit:0.##} kg/kiện   ·   {(type.allowRotation?"Được xoay":"Giữ nguyên hướng")}   ·   {(type.stackable?"Được xếp chồng":"Không xếp chồng")}",21,UiMuted);
            var row = Horizontal(item, "CargoActions"); MobileButton(row, "Sửa", () => { EditCargo(type); RefreshMobileUI(); }); MobileButton(row, "Xóa", () => RequestMobileCargoDelete(type), false, true);
        }
        if (!cargoFormVisible) return;
        var form = Vertical(content, "CargoForm", UiSurface, 10, 28);
        MobileText(form, editingTypeId == null ? "Thêm loại hàng" : "Sửa loại hàng", 31, UiText, FontStyle.Bold);
        MobileInput(form, "Mã hàng", cargoCode, v => cargoCode = v); MobileInput(form, "Tên / mô tả", cargoName, v => cargoName = v); MobileInput(form, "Dài (ô)", cargoLength, v => cargoLength = v, "", InputField.ContentType.IntegerNumber); MobileInput(form, "Rộng (ô)", cargoWidth, v => cargoWidth = v, "", InputField.ContentType.IntegerNumber); MobileInput(form, "Cao (ô)", cargoHeight, v => cargoHeight = v, "", InputField.ContentType.IntegerNumber);MobileInput(form,"Số lượng",cargoQuantity,v=>cargoQuantity=v,"",InputField.ContentType.IntegerNumber);MobileInput(form,"Trọng lượng mỗi kiện (kg)",cargoWeight,v=>cargoWeight=v,"0",InputField.ContentType.DecimalNumber);
        var rules=Horizontal(form,"CargoRules",10);MobileButton(rules,cargoAllowRotation?"Xoay: Có":"Xoay: Không",()=>{cargoAllowRotation=!cargoAllowRotation;formDirty=true;RefreshMobileUI(true);},cargoAllowRotation);MobileButton(rules,cargoStackable?"Xếp chồng: Có":"Xếp chồng: Không",()=>{cargoStackable=!cargoStackable;formDirty=true;RefreshMobileUI(true);},cargoStackable);
        var colorLabel = MobileText(form, "Màu loại hàng  ·  #" + ColorUtility.ToHtmlStringRGB(cargoColor), 23, UiMuted, FontStyle.Bold);
        var swatch = Surface(form, "ColorPreview", cargoColor); swatch.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;
        var colorGrid = Vertical(form, "ColorGrid", null, 8, 0);
        for (var rowIndex = 0; rowIndex < 6; rowIndex++)
        {
            var row = Horizontal(colorGrid, "ColorRow", 8);
            for (var columnIndex = 0; columnIndex < 8; columnIndex++)
            {
                var color = Color.HSVToRGB((columnIndex + .5f) / 8f, rowIndex < 3 ? 1f : .55f, new[] { .45f, .7f, 1f }[rowIndex % 3]);
                MobileColorSwatch(row, color, () => { cargoColor = color; swatch.color = color; colorLabel.text = "Màu loại hàng  ·  #" + ColorUtility.ToHtmlStringRGB(color); });
            }
        }
        var save = Horizontal(form, "CargoFormActions"); MobileButton(save, "Hủy", () => { ClearCargo(); RefreshMobileUI(); }); MobileButton(save, "LƯU LOẠI HÀNG", () => { SaveCargo(); RefreshMobileUI(); }, true);
    }

    void RequestMobileCargoDelete(CargoType type)
    {
        if (plan.placedCargo.Exists(x => x.cargoTypeId == type.id)) { status = "Không thể xóa vì loại hàng đang được sử dụng."; RefreshMobileUI(); return; }
        Confirm("Xóa loại hàng?", type.code + " sẽ bị xóa khỏi danh mục.", () => { var backup=JsonUtility.ToJson(plan);plan.cargoTypes.Remove(type);if(!TrySaveCurrent())plan=JsonUtility.FromJson<LoadingPlan>(backup);RefreshMobileUI(); });
    }

    void BuildMobileEditor()
    {
        var full = Object("EditorOverlay", mobileRoot); Stretch(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var overlay = Object("EditorSafeArea", full); ApplySafeArea(overlay);
        var header = Surface(overlay, "EditorHeader", new Color(1, 1, 1, .98f)).rectTransform; Stretch(header, new Vector2(0, 1), Vector2.one, new Vector2(0, -142), Vector2.zero);
        var back = MobileButton(header, "Quay lại", OpenDetail); var backRect = back.GetComponent<RectTransform>(); backRect.anchorMin = backRect.anchorMax = new Vector2(0, .5f); backRect.pivot = new Vector2(0, .5f); backRect.anchoredPosition = new Vector2(20, -10); backRect.sizeDelta = new Vector2(160, 88);
        var title = MobileText(header, plan.name, 33, UiText, FontStyle.Bold); Stretch(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(198, 38), new Vector2(-330, -10));
        var summary = MobileText(header, $"{plan.placedCargo.Count} kiện  ·  Tầng {layer + 1}/{plan.container.height}", 21, UiMuted); Stretch(summary.rectTransform, Vector2.zero, Vector2.one, new Vector2(198, 5), new Vector2(-330, -72));
        var undoButton = MobileButton(header, "Hoàn tác", Undo); var undoRect = undoButton.GetComponent<RectTransform>(); undoRect.anchorMin = undoRect.anchorMax = new Vector2(1, .5f); undoRect.pivot = new Vector2(1, .5f); undoRect.anchoredPosition = new Vector2(-168, -10); undoRect.sizeDelta = new Vector2(145, 88); undoButton.interactable = undo.Count > 0;
        var redoButton = MobileButton(header, "Làm lại", Redo); var redoRect = redoButton.GetComponent<RectTransform>(); redoRect.anchorMin = redoRect.anchorMax = new Vector2(1, .5f); redoRect.pivot = new Vector2(1, .5f); redoRect.anchoredPosition = new Vector2(-18, -10); redoRect.sizeDelta = new Vector2(145, 88); redoButton.interactable = redo.Count > 0;
        var panel = Surface(overlay, "EditorControls", new Color(1, 1, 1, .98f)).rectTransform; Stretch(panel, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 650));
        var content = MobileScroll(panel, 0, 0);
        StatusBanner(content, status);
        var cameras = Horizontal(content, "ViewMode", 8); MobileButton(cameras, "3D", ResetCamera, true); MobileButton(cameras, "Trên", TopView); MobileButton(cameras, "Trước", FrontView); MobileButton(cameras, "Bên", SideView);
        var modeRow = Horizontal(content, "PlacementMode", 12);
        MobileButton(modeRow, "Chạm trên 3D", () => SetPlacementInputMode(PlacementInputMode.Touch), placementInputMode == PlacementInputMode.Touch);
        MobileButton(modeRow, "Nút điều hướng", () => SetPlacementInputMode(PlacementInputMode.Controls), placementInputMode == PlacementInputMode.Controls);
        SectionHeader(content, "Chọn loại hàng");
        var editorSearch=Horizontal(content,"EditorCargoSearch",10);var editorSearchField=MobileInput(editorSearch,"",cargoSearch,v=>cargoSearch=v,"Tìm hàng...");editorSearchField.onEndEdit.AddListener(_=>RefreshMobileUI(true));MobileButton(editorSearch,"Tìm",()=>RefreshMobileUI(true),true);
        var editorFilters=Horizontal(content,"EditorCargoFilters",8);MobileButton(editorFilters,"Tất cả",()=>{cargoFilter=CargoFilter.All;RefreshMobileUI(true);},cargoFilter==CargoFilter.All);MobileButton(editorFilters,"Còn lại",()=>{cargoFilter=CargoFilter.Remaining;RefreshMobileUI(true);},cargoFilter==CargoFilter.Remaining);MobileButton(editorFilters,"Đã đủ",()=>{cargoFilter=CargoFilter.Complete;RefreshMobileUI(true);},cargoFilter==CargoFilter.Complete);
        if(plan.cargoTypes.Count==0)StatusBanner(content,"Chưa có loại hàng để xếp.");
        for (var i = 0; i < plan.cargoTypes.Count; i++) { var index = i; var type = plan.cargoTypes[i];if(!CargoMatchesFilter(type)||(cargoSearch.Length>0&&(type.code??"").IndexOf(cargoSearch,StringComparison.OrdinalIgnoreCase)<0&&(type.name??"").IndexOf(cargoSearch,StringComparison.OrdinalIgnoreCase)<0))continue;var placed=shipment.containers.Sum(c=>c.placedCargo.Count(x=>x.cargoTypeId==type.id));var remaining=ShipmentIntelligence.Remaining(shipment,type);var button=MobileButton(content, type.code + "  ·  " + type.name + "   " + type.length + "×" + type.width + "×" + type.height + $"   ·   Toàn chuyến {placed}/{type.quantity} · Còn {remaining}", () => BeginPlacement(index), placementMode && i == selectedType);button.interactable=remaining>0; }
        var layerRow = Horizontal(content, "Layer"); MobileButton(layerRow, "Hạ tầng", () => { layer = Mathf.Max(0, layer - 1); if (placementMode) { previewPosition.z = layer; SetPreview(previewPosition, false); } else RefreshMobileUI(true); }); MobileText(layerRow, "Tầng đáy " + (layer + 1) + "/" + plan.container.height, 27, UiText, FontStyle.Bold, TextAnchor.MiddleCenter); MobileButton(layerRow, "Nâng tầng", () => { layer = Mathf.Min(plan.container.height - 1, layer + 1); if (placementMode) { previewPosition.z = layer; SetPreview(previewPosition, false); } else RefreshMobileUI(true); });
        if (plan.cargoTypes.Count > 0)
        {
            var type = plan.cargoTypes[Mathf.Clamp(selectedType, 0, plan.cargoTypes.Count - 1)];
            var position = Vertical(content, "PositionController", UiPrimarySoft, 8, 24);
            MobileText(position, placementMode ? "Vị trí xem trước" : "Chọn loại hàng để bắt đầu xếp", 26, UiPrimary, FontStyle.Bold);
            MobileText(position, type.code + "  •  Kích thước " + type.length + " × " + type.width + " × " + type.height + " ô", 24, UiText);
            MobileText(position,$"Toàn chuyến đã xếp {shipment.containers.Sum(c=>c.placedCargo.Count(x=>x.cargoTypeId==type.id))}/{type.quantity} · Còn {ShipmentIntelligence.Remaining(shipment,type)}",23,UiMuted,FontStyle.Bold);
            var assist=Horizontal(position,"AssistedPlacement",10);var rotatePreview=MobileButton(assist,"XOAY 90°",RotateSelected);rotatePreview.interactable=type.allowRotation;MobileButton(assist,"ĐỀ XUẤT VỊ TRÍ",SuggestPlacement);MobileButton(assist,"XẾP HẾT CÒN LẠI",()=>Confirm("Xem trước xếp tự động?","Ứng dụng sẽ thử xếp tất cả hàng còn lại theo thứ tự ổn định. Bạn được xem trước rồi mới chấp nhận.",PreviewAutoFill,"Xem trước","Hủy"),true);
            var previewSize = GridPlacement.RotatedSize(new Vector3Int(type.length, type.width, type.height), rotation);
            MobileText(position, previewSize.z > 1 ? $"Chiếm tầng {previewPosition.z + 1}–{previewPosition.z + previewSize.z}" : $"Chiếm tầng {previewPosition.z + 1}", 23, UiMuted, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (placementInputMode == PlacementInputMode.Controls)
            {
                NumberStepper(position, "Cột", 0, previewPosition.x);
                NumberStepper(position, "Hàng", 1, previewPosition.y);
                NumberStepper(position, "Tầng", 2, previewPosition.z);
            }
            else MobileText(position, "Chạm trực tiếp vào ô trên mô hình 3D để di chuyển preview.", 24, UiMuted);
            MobileText(position, status.StartsWith("✓") ? "✓ Vị trí hợp lệ" : status, 24, status.StartsWith("✓") ? UiPrimary : UiDanger, FontStyle.Bold);
            var placementActions = Horizontal(position, "PlacementActions"); MobileButton(placementActions, "Hủy", CancelPreview);
            var placeButton = MobileButton(placementActions, "ĐẶT HÀNG", ConfirmPreview, true);
            placeButton.interactable = placementMode && GridPlacement.TryPlace(plan, type, previewPosition, rotation, selectedId, out _);
        }
        var selected = FindPlaced(selectedId);
        if(selected!=null)
        {
            var selectedTypeData=plan.cargoTypes.Find(x=>x.id==selected.cargoTypeId);var selectedPanel=Vertical(content,"SelectedCargo",UiSurface,7,22);
            MobileText(selectedPanel,"Kiện đang chọn",26,UiPrimary,FontStyle.Bold);MobileText(selectedPanel,(selectedTypeData?.code??"?")+"  ·  Cột "+(selected.position.x+1)+", Hàng "+(selected.position.y+1)+", Tầng "+(selected.position.z+1)+"  ·  "+selected.rotation+"°",23,UiText);if(selectedTypeData!=null)MobileText(selectedPanel,$"Kích thước {selected.size.x}×{selected.size.y}×{selected.size.z} ô · {selectedTypeData.weightPerUnit:0.##} kg",21,UiMuted);
            var tools=Horizontal(selectedPanel,"SelectedActions");MobileButton(tools,"Tập trung",FocusSelected);MobileButton(tools,"Di chuyển",MoveSelected);MobileButton(tools,"Xoay",RotateSelected);MobileButton(tools,"Xóa",DeleteSelected,false,true);
        }
        var saveRow = Horizontal(content, "SaveEditor"); MobileButton(saveRow, "Hủy xem trước", CancelPreview); MobileButton(saveRow, "LƯU SƠ ĐỒ", () => { TrySaveCurrent(); RefreshMobileUI(); }, true);
    }

    void BuildMobileSettings()
    {
        var content = BuildPage("Cài đặt", "Dữ liệu và cập nhật ứng dụng", false, OpenHome);
        SectionHeader(content,"Dữ liệu");
        var data=Vertical(content,"DataSettings",UiSurface,8,24);MobileText(data,"Sao lưu và khôi phục",29,UiText,FontStyle.Bold);MobileText(data,"Bản sao lưu toàn bộ được kiểm tra trước khi khôi phục và không ghi đè dữ liệu hiện tại.",22,UiMuted);var dataActions=Horizontal(data,"DataActions");MobileButton(dataActions,"Khôi phục",BeginImportBackup);MobileButton(dataActions,"Sao lưu toàn bộ",ExportFullBackup,true);
        SectionHeader(content,"Cập nhật ứng dụng");
        var update=Vertical(content,"UpdateSettings",UiSurface,8,24);MobileText(update,"Phiên bản hiện tại  ·  "+Application.version,27,UiText,FontStyle.Bold);MobileText(update,string.IsNullOrWhiteSpace(updateStatus)?"Tự động kiểm tra một lần khi mở ứng dụng.":updateStatus,22,string.IsNullOrWhiteSpace(updateStatus)?UiMuted:UiPrimary);var updateActions=Horizontal(update,"UpdateActions");var check=MobileButton(updateActions,updateChecking?"Đang kiểm tra…":"KIỂM TRA CẬP NHẬT",()=>StartCoroutine(CheckForUpdates(false)),true);check.interactable=!updateChecking;if(latestUpdate!=null&&latestUpdate.IsUpdateAvailable)MobileButton(updateActions,"CẬP NHẬT NGAY",OpenLatestUpdate,true);
        SectionHeader(content,"Ứng dụng");
        var about = Vertical(content, "About", UiSurface, 8, 24); MobileText(about, "Container Loading", 29, UiText, FontStyle.Bold); MobileText(about, "Phiên bản " + Application.version + "\nCông cụ lập phương án xếp container chuyên nghiệp.", 22, UiMuted);
    }

    bool CargoMatchesFilter(CargoType type){var placed=shipment.containers.Sum(c=>c.placedCargo.Count(x=>x.cargoTypeId==type.id));return cargoFilter==CargoFilter.All||cargoFilter==CargoFilter.Remaining&&placed<type.quantity||cargoFilter==CargoFilter.Complete&&placed>=type.quantity;}

    void ShowAutoFillPreview(int count)
    {
        var stats=PlanIntelligence.Statistics(plan);var panel=ModalPanel("Xem trước xếp tự động",$"Đã thử xếp thêm {count} kiện.\nTiến độ mới: {stats.PlacedQuantity}/{stats.TotalQuantity} kiện.\nChỉ lưu khi bạn chọn Chấp nhận.",650);MobileButton(panel,"CHẤP NHẬN",()=>{CloseModal();AcceptAutoFill();},true);MobileButton(panel,"HỦY BẢN XEM TRƯỚC",()=>{CloseModal();CancelAutoFill();},false,true);
    }

    string FormatUpdatedAt(string value) => DateTime.TryParse(value, out var parsed) ? parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "Không rõ";

    void ShowPlanHealth()
    {
        var report=PlanIntelligence.Check(plan);var lines=new System.Collections.Generic.List<string>{report.Summary};
        lines.AddRange(report.errors.Select(x=>"Lỗi · "+x));lines.AddRange(report.warnings.Select(x=>"Cảnh báo · "+x));lines.AddRange(report.information.Select(x=>"Thông tin · "+x));
        var panel=ModalPanel("Kiểm tra phương án",string.Join("\n",lines),780);MobileButton(panel,"Đóng",CloseModal,true);
    }

    RectTransform ModalPanel(string titleText, string message, float height = 620)
    {
        if (modalShade) Destroy(modalShade);
        modalShade = Surface(mobileRoot, "ModalShade", new Color(0, 0, 0, .58f)).gameObject;
        var shade = modalShade.GetComponent<RectTransform>(); Stretch(shade, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var panel = Vertical(shade, "ModalPanel", UiSurface, 14, 30); panel.anchorMin = new Vector2(.04f, .5f); panel.anchorMax = new Vector2(.96f, .5f); panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(0, Mathf.Min(height, 920)); panel.anchoredPosition = Vector2.zero;
        MobileText(panel, titleText, 34, UiText, FontStyle.Bold);
        if (!string.IsNullOrWhiteSpace(message)) MobileText(panel, message, 24, UiMuted);
        return panel;
    }

    void ShowPlanActions(string path, LoadingPlan data)
    {
        var panel = ModalPanel(data.name, "Tùy chọn phương án", 650);
        MobileButton(panel, "Đổi tên / chỉnh sửa", () => { CloseModal(); OpenPlan(path); BeginEditContainer(); RefreshMobileUI(); });
        MobileButton(panel, "Sao lưu JSON", () => { CloseModal(); OpenPlan(path); ExportPlanData(); });
        MobileButton(panel, "Nhân bản", () => { CloseModal(); ShowDuplicateOptions(data); });
        MobileButton(panel, "Xóa phương án", () => { CloseModal(); Confirm("Xóa phương án?", "Toàn bộ sơ đồ xếp hàng sẽ bị xóa.", () => { PlanPersistence.Delete(path); status = "Đã xóa phương án."; OpenHome(); }); }, false, true);
        MobileButton(panel, "Đóng", CloseModal);
    }

    void ShowDuplicateOptions(LoadingPlan data){var panel=ModalPanel("Nhân bản phương án","Chọn nội dung cần sao chép. Mã đơn, số container và niêm phong luôn được để trống.",690);MobileButton(panel,"KÈM SƠ ĐỒ XẾP",()=>{CloseModal();DuplicatePlan(data,true);},true);MobileButton(panel,"CHỈ DỮ LIỆU HÀNG",()=>{CloseModal();DuplicatePlan(data,false);});MobileButton(panel,"Hủy",CloseModal);}

    void ShowPdfExportDialog()
    {
        var used = UsedVolume(plan); var capacity = Mathf.Max(1, plan.container.length * plan.container.width * plan.container.height);
        Confirm("Xuất báo cáo PDF", "Phương án: " + plan.name + "\nMã đơn hàng: " + (string.IsNullOrWhiteSpace(plan.orderReference) ? "Chưa có" : plan.orderReference) + "\nContainer: " + plan.container.name + "\nTổng kiện: " + plan.placedCargo.Count + "\nSử dụng: " + used + "/" + capacity + " ô", ExportPdf, "Tạo PDF", "Đóng");
    }

    void ShowPdfReadyDialog()
    {
        var panel = ModalPanel("PDF đã được tạo", "Tên tệp có chứa tên container và được ưu tiên lưu trong thư mục Documents.", 560);
        var actions = Horizontal(panel, "PdfReadyActions"); MobileButton(actions, "Mở PDF", OpenPdf, true); MobileButton(actions, "Chia sẻ", SharePdf);
        MobileButton(panel, "Đóng", CloseModal);
    }

    void ShowUnsavedDialog(Action discard)
    {
        var panel = ModalPanel("Bạn có thay đổi chưa lưu", "Lưu thay đổi trước khi rời khỏi trang?", 610);
        MobileButton(panel, "LƯU", () => { CloseModal(); if (screen == ScreenMode.Container) SaveContainer(); else { SaveCargo(); if (!formDirty) OpenDetail(); } }, true);
        MobileButton(panel, "BỎ THAY ĐỔI", () => { CloseModal(); discard(); }, false, true);
        MobileButton(panel, "TIẾP TỤC CHỈNH SỬA", CloseModal);
    }

    void RequestBackNavigation()
    {
        if (screen == ScreenMode.Editor && placementMode) { CancelPreview(); return; }
        Action leave = () => { formDirty = false; if (screen == ScreenMode.Editor || screen == ScreenMode.Cargo || screen == ScreenMode.Container && editingContainer) OpenDetail(); else OpenHome(); };
        if ((screen == ScreenMode.Container || screen == ScreenMode.Cargo) && formDirty)
            ShowUnsavedDialog(leave);
        else leave();
    }

    void Confirm(string titleText, string message, Action accepted, string acceptedText = "Xóa", string cancelledText = "Hủy")
    {
        var dialog = ModalPanel(titleText, message, message != null && message.Length > 280 ? 820 : 560);
        var row = Horizontal(dialog, "DialogActions"); MobileButton(row, cancelledText, CloseModal); var destructive=acceptedText=="Xóa";MobileButton(row, acceptedText, () => { CloseModal(); accepted(); }, !destructive, destructive);
    }

    void CloseModal(){if(modalShade){Destroy(modalShade);modalShade=null;}}
}
