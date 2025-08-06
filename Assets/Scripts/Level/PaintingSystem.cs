using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;

public class PaintingSystem : MonoBehaviour
{
    public static PaintingSystem Instance;

    [Header("UI组件")]
    public Canvas paintingCanvas;
    public RawImage leftDrawingArea, rightPreviewArea;
    public Button redButton, blueButton, greenButton, confirmButton, revertButton, editButton;
    public Slider saturationSlider, redSlider, blueSlider, greenSlider;
    public RectTransform leftPanel, rightPanel;
    public Canvas photoCanvas;

    [Header("绘画设置")]
    public int canvasSize = 512;
    public int brushSize = 20;

    [Header("预览相机设置")]
    public Camera previewCamera;
    public float previewCameraHeight = 50f, previewCameraPadding = 5f;

    private Color currentColor = Color.red;
    private float currentSaturation = 1f;
    private Texture2D canvasTexture;
    private RenderTexture previewRenderTexture;
    private LevelBlueprint currentBlueprint;
    private bool isDrawing = false, isMouseOverDrawingArea = false, isEditMode = false;
    private Vector2 lastDrawPosition;
    private int selectedLineIndex = -1;
    private int remainingPOIPaint, remainingLargePropPaint, remainingSmallPropPaint;
    private List<PaintLine> paintLines = new List<PaintLine>();
    private List<Vector2> currentLinePoints = new List<Vector2>();
    private float currentLineSaturation;
    private int currentLinePaintConsumed = 0;
    private Transform newLevelRoot;

    // 线条信息
    [System.Serializable]
    public class PaintLine
    {
        public List<Vector2> points;
        public Color color;
        public float saturation;
        public PropType propType;
        public int paintConsumed;

        public PaintLine(List<Vector2> linePoints, Color lineColor, float lineSaturation, int paintUsed)
        {
            points = new List<Vector2>(linePoints);
            color = lineColor;
            saturation = lineSaturation;
            propType = GetPropTypeFromColor(lineColor);
            paintConsumed = paintUsed;
        }

        // 根据颜色获取对应的prop类型
        private PropType GetPropTypeFromColor(Color color)
        {
            if (color.r > color.g && color.r > color.b) return PropType.POI;
            else if (color.b > color.r && color.b > color.g) return PropType.LargeProp;
            else return PropType.SmallProp;
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeUI();
        SetupDrawingArea();
        SetupPreviewCamera();
    }

    // 初始化UI组件
    private void InitializeUI()
    {
        redButton.onClick.AddListener(() => SetColor(Color.red));
        blueButton.onClick.AddListener(() => SetColor(Color.blue));
        greenButton.onClick.AddListener(() => SetColor(Color.green));
        confirmButton.onClick.AddListener(ConfirmPainting);
        revertButton.onClick.AddListener(RevertLastLine);
        editButton.onClick.AddListener(ToggleEditMode);

        if (saturationSlider != null)
        {
            saturationSlider.onValueChanged.AddListener(OnSaturationChanged);
            saturationSlider.minValue = 0.1f;
            saturationSlider.maxValue = 1f;
            saturationSlider.value = 1f;
        }

        if (paintingCanvas != null) paintingCanvas.gameObject.SetActive(false);
    }

    // 初始化绘画区域
    private void SetupDrawingArea()
    {
        canvasTexture = new Texture2D(canvasSize, canvasSize, TextureFormat.RGB24, false);
        ClearCanvas();
        if (leftDrawingArea != null)
        {
            leftDrawingArea.texture = canvasTexture;
        }
    }

    // 初始化预览相机
    private void SetupPreviewCamera()
    {
        previewCamera.orthographic = true;
        previewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        previewCamera.enabled = false;

        previewRenderTexture = new RenderTexture(512, 512, 24);
        previewCamera.targetTexture = previewRenderTexture;
        if (rightPreviewArea != null) rightPreviewArea.texture = previewRenderTexture;
    }

    // 开始绘画
    // 根据关卡蓝图初始化
    public void StartPainting(LevelBlueprint blueprint, Transform levelRoot)
    {
        if (blueprint == null) return;

        currentBlueprint = blueprint;
        newLevelRoot = levelRoot;

        remainingPOIPaint = blueprint.maxPOIPaint;
        remainingLargePropPaint = blueprint.maxLargePropPaint;
        remainingSmallPropPaint = blueprint.maxSmallPropPaint;

        if (redSlider != null) redSlider.maxValue = blueprint.maxPOIPaint;
        if (blueSlider != null) blueSlider.maxValue = blueprint.maxLargePropPaint;
        if (greenSlider != null) greenSlider.maxValue = blueprint.maxSmallPropPaint;

        UpdatePaintMeters();

        paintLines.Clear();
        if (paintingCanvas != null) paintingCanvas.gameObject.SetActive(true);
        ClearCanvas();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        previewCamera.enabled = true;
        // 根据地形renderer的Bounds设置预览相机位置和大小
        Renderer terrainRenderer = levelRoot.GetComponentInChildren<Renderer>();
        if (terrainRenderer != null)
        {
            Bounds terrainBounds = terrainRenderer.bounds;
            float maxHorizontalSize = Mathf.Max(terrainBounds.size.x, terrainBounds.size.z);
            previewCamera.orthographicSize = (maxHorizontalSize / 2f) + previewCameraPadding;

            Vector3 terrainCenter = terrainBounds.center;
            Vector3 previewPos = new Vector3(terrainCenter.x, terrainCenter.y + previewCameraHeight, terrainCenter.z);
            previewCamera.transform.position = previewPos;
        }
        UpdatePreview();

        photoCanvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (paintingCanvas != null && !paintingCanvas.gameObject.activeSelf) return;
        HandleDrawing();
        HandleEditMode();
    }

    // 处理绘画中输入
    private void HandleDrawing()
    {
        if (isEditMode) return;

        isMouseOverDrawingArea = IsMouseOverDrawingArea();

        // 鼠标在绘画区域内、按下、有剩余颜料
        // 则画新线条
        if (isMouseOverDrawingArea && Input.GetMouseButtonDown(0) && HasPaintRemaining())
        {
            Vector2 localPoint = GetLocalPointInDrawingArea();
            if (IsValidDrawingPosition(localPoint))
            {
                StartNewLine();
                isDrawing = true;
                lastDrawPosition = LocalPointToTextureCoord(localPoint);
                DrawPointAtPosition(lastDrawPosition, true);
                currentLinePoints.Add(lastDrawPosition);
            }
        }
        // 鼠标按住左键、正在绘画、有剩余颜料
        // 则继续画线条
        else if (Input.GetMouseButton(0) && isDrawing && HasPaintRemaining())
        {
            Vector2 localPoint = GetLocalPointInDrawingArea();
            if (IsValidDrawingPosition(localPoint))
            {
                Vector2 currentPos = LocalPointToTextureCoord(localPoint);
                DrawLine(lastDrawPosition, currentPos, true);
                lastDrawPosition = currentPos;
                currentLinePoints.Add(currentPos);
            }
        }
        // 如果鼠标抬起或离开绘画区域且正在绘画
        // 则结束当前线条并重置正在绘画的状态
        if ((Input.GetMouseButtonUp(0) || !isMouseOverDrawingArea) && isDrawing)
        {
            FinishCurrentLine();
            isDrawing = false;
        }
    }

    // 开始绘制新线条
    private void StartNewLine()
    {
        currentLinePoints.Clear();
        currentSaturation = saturationSlider.value;
        currentLineSaturation = currentSaturation;
        currentLinePaintConsumed = 0;
    }

    // 结束当前线条绘制
    private void FinishCurrentLine()
    {
        if (currentLinePoints.Count > 0)
        {
            PaintLine newLine = new PaintLine(currentLinePoints, currentColor, currentLineSaturation, currentLinePaintConsumed); // 新建线条
            paintLines.Add(newLine);
            if (LevelGenerator.Instance != null)
                LevelGenerator.Instance.UpdateLevelFromPaintLine(newLevelRoot, currentBlueprint, newLine);
            UpdatePreview();
        }
    }

    // 切换编辑模式
    private void ToggleEditMode()
    {
        isEditMode = !isEditMode;
        saturationSlider.gameObject.SetActive(isEditMode);
        if (!isEditMode) saturationSlider.value = 1f;
        selectedLineIndex = -1;
        SetColorButtonsInteractable(!isEditMode);
        RedrawCanvas(isEditMode);
    }

    // 编辑模式下禁用颜色选择
    private void SetColorButtonsInteractable(bool interactable)
    {
        if (redButton != null) redButton.interactable = interactable;
        if (blueButton != null) blueButton.interactable = interactable;
        if (greenButton != null) greenButton.interactable = interactable;
    }

    // 处理编辑模式下的输入
    private void HandleEditMode()
    {
        if (!isEditMode || !Input.GetMouseButtonDown(0) || !IsMouseOverDrawingArea()) return;

        Vector2 localPoint = GetLocalPointInDrawingArea();
        Vector2 textureCoord = LocalPointToTextureCoord(localPoint);
        int clickedLineIndex = FindLineIndexAtPosition(textureCoord);

        if (clickedLineIndex >= 0)
        {
            selectedLineIndex = clickedLineIndex;
            if (saturationSlider != null) saturationSlider.value = paintLines[clickedLineIndex].saturation;
            RedrawCanvas(true);
        }
    }

    // 编辑模式下，查找鼠标位置附近的线条，返回线条索引
    private int FindLineIndexAtPosition(Vector2 position)
    {
        float minDistance = float.MaxValue;
        int closestLineIndex = -1;
        float threshold = brushSize * 1.5f;

        // 遍历所有线条，找到距离鼠标位置最近的线条
        for (int i = 0; i < paintLines.Count; i++)
        {
            foreach (Vector2 point in paintLines[i].points)
            {
                float distance = Vector2.Distance(position, point);
                if (distance < threshold && distance < minDistance)
                {
                    minDistance = distance;
                    closestLineIndex = i;
                }
            }
        }

        return closestLineIndex;
    }

    // 编辑模式下，饱和度变化时，重新生成线条对应的prop并重绘画布
    private void OnSaturationChanged(float newSaturation)
    {
        if (isEditMode && selectedLineIndex >= 0 && selectedLineIndex < paintLines.Count)
        {
            paintLines[selectedLineIndex].saturation = newSaturation;
            if (LevelGenerator.Instance != null)
                LevelGenerator.Instance.UpdatePropsForLine(selectedLineIndex, paintLines[selectedLineIndex]);
            RedrawCanvas(true);
            UpdatePreview();
        }
        else currentSaturation = newSaturation;
    }

    // 重绘画布
    // 预留画布整体变化的可能操作
    private void RedrawCanvas(bool withHighlights = false)
    {
        ClearCanvas();

        for (int i = 0; i < paintLines.Count; i++)
        {
            PaintLine line = paintLines[i];
            Color drawColor = GetDrawColor(line); // 获取当前线条颜色

            // 重绘时，提高选中线条的亮度
            if (withHighlights && i == selectedLineIndex)
                drawColor = Color.Lerp(drawColor, Color.white, 0.3f);

            // 绘制线条上的所有点
            for (int j = 0; j < line.points.Count; j++)
            {
                DrawPointAtPosition(line.points[j], false, drawColor);
                if (j > 0) DrawLine(line.points[j - 1], line.points[j], false, drawColor);
            }
        }

        canvasTexture.Apply();
    }

    // 撤销最后一条线条
    private void RevertLastLine()
    {
        if (paintLines.Count > 0)
        {
            PaintLine lineToRemove = paintLines[paintLines.Count - 1];
            int lineIndex = paintLines.Count - 1;
            RestorePaint(lineToRemove);

            if (LevelGenerator.Instance != null)
                LevelGenerator.Instance.RemoveOrClearPropsForLine(lineIndex, 0); // 移除线条对应的Props及mapping

            paintLines.RemoveAt(paintLines.Count - 1);
            if (selectedLineIndex >= paintLines.Count) selectedLineIndex = -1;

            RedrawCanvas(isEditMode);
            UpdatePreview();
            UpdatePaintMeters();
        }
    }

    // 恢复被撤销的线条的颜料
    private void RestorePaint(PaintLine lineToRemove)
    {
        int paintToRestore = lineToRemove.paintConsumed;
        switch (lineToRemove.propType)
        {
            case PropType.POI:
                remainingPOIPaint = Mathf.Min(currentBlueprint.maxPOIPaint, remainingPOIPaint + paintToRestore);
                break;
            case PropType.LargeProp:
                remainingLargePropPaint = Mathf.Min(currentBlueprint.maxLargePropPaint, remainingLargePropPaint + paintToRestore);
                break;
            case PropType.SmallProp:
                remainingSmallPropPaint = Mathf.Min(currentBlueprint.maxSmallPropPaint, remainingSmallPropPaint + paintToRestore);
                break;
        }
    }

    // 清空画布
    private void ClearCanvas()
    {
        Color[] pixels = new Color[canvasSize * canvasSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        canvasTexture.SetPixels(pixels);
        canvasTexture.Apply();
    }

    // 更新预览界面
    private void UpdatePreview()
    {
        if (previewCamera != null && previewCamera.enabled) previewCamera.Render();
    }

    // 检查鼠标是否在绘画区域内
    private bool IsMouseOverDrawingArea()
    {
        return leftDrawingArea != null && RectTransformUtility.RectangleContainsScreenPoint(leftDrawingArea.rectTransform, Input.mousePosition);
    }

    // 获取鼠标在绘画区域内的本地坐标
    private Vector2 GetLocalPointInDrawingArea()
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(leftDrawingArea.rectTransform, Input.mousePosition, null, out localPoint);
        return localPoint;
    }

    // 检查本地坐标是否在绘画区域内
    private bool IsValidDrawingPosition(Vector2 localPoint)
    {
        if (leftDrawingArea == null) return false;
        Rect rect = leftDrawingArea.rectTransform.rect;
        return localPoint.x >= rect.xMin && localPoint.x <= rect.xMax && localPoint.y >= rect.yMin && localPoint.y <= rect.yMax;
    }

    // 将本地坐标转换为纹理坐标
    private Vector2 LocalPointToTextureCoord(Vector2 localPoint)
    {
        Rect rect = leftDrawingArea.rectTransform.rect;
        Vector2 normalizedPoint = new Vector2((localPoint.x - rect.x) / rect.width, (localPoint.y - rect.y) / rect.height);
        return new Vector2(normalizedPoint.x * canvasSize, normalizedPoint.y * canvasSize);
    }

    // 在指定位置绘制点
    private void DrawPointAtPosition(Vector2 position, bool consumePaint = false, Color? overrideColor = null)
    {
        Color drawColor = overrideColor ?? (consumePaint ? GetDrawColor() : Color.white);

        int x = Mathf.RoundToInt(position.x);
        int y = Mathf.RoundToInt(position.y);
        int halfBrush = brushSize / 2;

        for (int i = -halfBrush; i <= halfBrush; i++)
        {
            for (int j = -halfBrush; j <= halfBrush; j++)
            {
                if (i * i + j * j <= halfBrush * halfBrush)
                {
                    int pixelX = Mathf.Clamp(x + i, 0, canvasSize - 1);
                    int pixelY = Mathf.Clamp(y + j, 0, canvasSize - 1);
                    canvasTexture.SetPixel(pixelX, pixelY, drawColor);
                }
            }
        }

        // 绘画模式下，需要消耗颜料
        // 编辑模式下，重绘不需要消耗颜料
        if (consumePaint)
        {
            ConsumePaint();
            canvasTexture.Apply();
            UpdatePaintMeters();
        }
    }

    // 根据起始和结束点绘制线条
    private void DrawLine(Vector2 from, Vector2 to, bool consumePaint = false, Color? overrideColor = null)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.RoundToInt(distance);

        for (int i = 0; i <= steps; i++)
        {
            float t = steps > 0 ? (float)i / steps : 0;
            Vector2 point = Vector2.Lerp(from, to, t);
            DrawPointAtPosition(point, consumePaint, overrideColor); // 调用绘制点的方法
        }
    }

    // 获取当前绘画颜色，考虑饱和度
    // 如果传入了线条，则使用线条的饱和度
    private Color GetDrawColor(PaintLine line = null)
    {
        if (line == null)
        {
            Color.RGBToHSV(currentColor, out float h, out float s, out float v);
            return Color.HSVToRGB(h, currentSaturation, v);
        }
        else
        {
            Color.RGBToHSV(line.color, out float h, out float s, out float v);
            return Color.HSVToRGB(h, line.saturation, v);
        }
    }

    // 检查当前颜色对应的颜料是否还有剩余
    private bool HasPaintRemaining()
    {
        if (currentColor.r > currentColor.g && currentColor.r > currentColor.b) return remainingPOIPaint > 0;
        else if (currentColor.b > currentColor.r && currentColor.b > currentColor.g) return remainingLargePropPaint > 0;
        else return remainingSmallPropPaint > 0;
    }

    // 消耗当前颜色对应的颜料
    private void ConsumePaint()
    {
        if (currentColor.r > currentColor.g && currentColor.r > currentColor.b)
            remainingPOIPaint = Mathf.Max(0, remainingPOIPaint - 1);
        else if (currentColor.b > currentColor.r && currentColor.b > currentColor.g)
            remainingLargePropPaint = Mathf.Max(0, remainingLargePropPaint - 1);
        else
            remainingSmallPropPaint = Mathf.Max(0, remainingSmallPropPaint - 1);

        currentLinePaintConsumed++;
    }

    // 设置当前绘画颜色
    private void SetColor(Color color)
    {
        if (!isEditMode) currentColor = color;
    }

    // 更新颜料计量条
    private void UpdatePaintMeters()
    {
        if (redSlider != null) redSlider.value = remainingPOIPaint;
        if (blueSlider != null) blueSlider.value = remainingLargePropPaint;
        if (greenSlider != null) greenSlider.value = remainingSmallPropPaint;
    }

    // 确认绘画完成，退出绘画模式
    private void ConfirmPainting()
    {
        if (canvasTexture == null) return;

        isEditMode = false;
        selectedLineIndex = -1;
        previewCamera.enabled = false;

        if (paintingCanvas != null) paintingCanvas.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        photoCanvas.gameObject.SetActive(true);
        LevelGenerator.Instance.OnPaintingCompleted();        
    }
    
    // 检查是否正在绘画模式中
    public bool IsInPaintingMode()
    {
        return paintingCanvas != null && paintingCanvas.gameObject.activeSelf;
    }
}