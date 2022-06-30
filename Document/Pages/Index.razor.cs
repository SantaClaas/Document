using System.Collections.Immutable;
using System.Numerics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Document.Pages;

public partial class Index
{
    private ElementReference _pdfCanvasReference;
    private IJSObjectReference? _helperModule;
    private CanvasDimensions _canvasSize;
    private bool _isMouseDown;
    private MousePosition _lastMousePosition;
    private ImmutableArray<IJSObjectReference> _rectanglePaths;
    private ElementReference _rectanglesCanvasReference;
    private IJSObjectReference? _rectanglesDrawingContext;


    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool isFirstRender)
    {
        if (!isFirstRender) return;
        Logger.LogInformation("Loading helper module");

        try
        {
            _helperModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Pages/Index.razor.js");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while loading helper module");
            return;
        }

        Logger.LogInformation("Loaded helper module. Loading PDF");
        IJSObjectReference pdf;
        try
        {
            pdf = await _helperModule.InvokeAsync<IJSObjectReference>("loadDocument",
                "compressed.tracemonkey-pldi-09.pdf");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while loading PDF");
            return;
        }

        Logger.LogInformation("Loaded PDF. Loading page one");
        IJSObjectReference pageOne;
        try
        {
            pageOne = await pdf.InvokeAsync<IJSObjectReference>("getPage", 1);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while loading page one");
            return;
        }

        Logger.LogInformation("Loaded page one. Getting viewport");

        IJSObjectReference viewport;
        try
        {
            viewport = await pageOne.InvokeAsync<IJSObjectReference>("getViewport", new { scale = 1.5 });
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while loading viewport");
            return;
        }

        Logger.LogInformation("Loaded viewport. Loading dimensions");
        try
        {
            var getHeightTask = _helperModule.InvokeAsync<double>("getProperty", "height", viewport).AsTask();
            var getWidthTask = _helperModule.InvokeAsync<double>("getProperty", "width", viewport).AsTask();
            await Task.WhenAll(getHeightTask, getWidthTask);
            _canvasSize = new CanvasDimensions(getHeightTask.Result, getWidthTask.Result);
            StateHasChanged();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while loading dimensions");
            return;
        }

        Logger.LogInformation("Got dimensions {Dimensions}. Loading canvas context", _canvasSize);

        IJSObjectReference drawingContext;
        try
        {
            var canvas = await _helperModule.InvokeAsync<IJSObjectReference>("identity", _pdfCanvasReference);
            drawingContext = await canvas.InvokeAsync<IJSObjectReference>("getContext", "2d");
            
            //TODO don't await here because we just initialize it
            // Also initialize drawing context for drawing rectangles here
            var rectanglesCanvas = await _helperModule.InvokeAsync<IJSObjectReference>("identity", _rectanglesCanvasReference);
            _rectanglesDrawingContext = await rectanglesCanvas.InvokeAsync<IJSObjectReference>("getContext", "2d");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while loading canvas context");
            return;
        }

        Logger.LogInformation("Loaded canvas context. Rendering PDF");
        // Default transform. If this would be a product we would need to adjust it for HiDPI-screens
        float?[] transform = { 1, 0, 0, 1, 0, 0 };
        PageRenderParameters parameters = new(drawingContext, viewport, transform);
        try
        {
            await pageOne.InvokeVoidAsync("render", parameters);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while rendering page one");
            return;
        }

        Logger.LogInformation("Rendered page one");
    }

    private readonly record struct CanvasDimensions(double Height, double Width);

    private readonly record struct MousePosition(double X, double Y);


    private void OnMouseDown(MouseEventArgs arguments)
    {
        Logger.LogInformation("Mouse down {Arguments}", arguments);

        // I think offset is what we actually want. Otherwise we would need to calculate it with the bounding box / 
        // coordinates of the canvas on the page
        _lastMousePosition = new MousePosition(arguments.OffsetX, arguments.OffsetY);
        _isMouseDown = true;
    }

    private void OnMouseUp(MouseEventArgs arguments)
    {
        Logger.LogInformation("Mouse up {Arguments}", arguments);
        _isMouseDown = false;
    }

    private async Task OnMouseMove(MouseEventArgs arguments)
    {
        if (!_isMouseDown) return;

        if (_rectanglesDrawingContext is null)
        {
            Logger.LogWarning("Canvas drawing context is not yet assigned. Cannot draw rectangle");
            return;
        }

        

        Logger.LogInformation("Begging path on canvas drawing context");
        try
        {
            await _rectanglesDrawingContext.InvokeVoidAsync("beginPath");
        }
        catch (Exception exception)
        {
            Logger.LogInformation(exception, "Error while beginning path on canvas drawing context");
            return;
        }

        Logger.LogInformation("Began path on canvas drawing context. Adding rectangle");
        var width = arguments.OffsetX - _lastMousePosition.X;
        var height = arguments.OffsetY - _lastMousePosition.Y;

        try
        {
            await _rectanglesDrawingContext.InvokeVoidAsync("rect", _lastMousePosition.X, _lastMousePosition.Y, width,
                height);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while adding rectangle");
            return;
        }

        Logger.LogInformation("Added rectangle. Setting stroke style");

        try
        {
            await _helperModule.InvokeVoidAsync("setProperty", "strokeStyle", "black", _rectanglesDrawingContext);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while setting stroke style");
            return;
        }

        Logger.LogInformation("Set stroke style. Setting line width");

        try
        {
            await _helperModule.InvokeVoidAsync("setProperty", "lineWidth", 2, _rectanglesDrawingContext);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while line width style");
            return;
        }
        
        // Clear before stroke to reduce time when nothing is drawn
        Logger.LogInformation("Clearing canvas");

        try
        {
            await _rectanglesDrawingContext.InvokeVoidAsync("clearRect", 0, 0, _canvasSize.Width, _canvasSize.Height);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while clearing");
            return;
        }
        
        Logger.LogInformation("Set line width. Stroking path");

        try
        {
            await _rectanglesDrawingContext.InvokeVoidAsync("stroke");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error while stroking path");
            return;
        }

        Logger.LogInformation("Stroke path");
    }
}

internal record PageRenderParameters(IJSObjectReference CanvasContext, IJSObjectReference Viewport,
    float?[]? Transform);