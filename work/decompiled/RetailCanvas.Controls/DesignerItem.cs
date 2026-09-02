using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RetailCanvas.Models;

namespace RetailCanvas.Controls;

public sealed class DesignerItem : ContentControl
{
	private readonly Grid _root;

	private readonly Border _selectionBorder;

	private FrameworkElement _visual;

	private readonly List<Thumb> _handles = new List<Thumb>();

	private Point _dragStart;

	private Point _pressScreen;

	private double _originalLeft;

	private double _originalTop;

	private bool _moving;

	private bool _pointerPressed;

	private bool _dragInitiated;

	private bool _resizing;

	private Point _resizeStartPointer;

	private Rect _resizeStartRect;

	private double _resizeStartFontSizePt;

	private double _resizeStartCharacterSpacing;

	private double _resizeStartLineSpacingPt;

	private double _resizeStartLineHeight;

	private double _resizeStartOutlineThicknessPt;

	private double _resizeStartExtrusionDepthPt;

	private string _resizeDirection = string.Empty;

	private double _pendingRotation;

	private double _rotationStart;

	private bool _rotating;

	private bool _exportMode;

	private bool _isSelected;

	private double _displayZoom = 1.0;

	public CanvasElementModel Model { get; }

	public Func<DesignerItem, Point, Point>? SnapPosition { get; set; }

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			_isSelected = value;
			UpdateSelection(value);
		}
	}

	public double DisplayZoom
	{
		get => _displayZoom;
		set
		{
			_displayZoom = Math.Clamp(value, 0.25, 4.0);
			UpdateHandleMetrics();
		}
	}

	public event EventHandler<DesignerItemSelectionEventArgs>? SelectionRequested;

	public event EventHandler? ChangeStarted;

	public event EventHandler? ModelChanged;

	public event EventHandler? ChangeCompleted;

	public event EventHandler? MoveStarted;

	public event EventHandler<DesignerItemMoveEventArgs>? MovePreview;

	public event EventHandler? MoveFinished;

	public event EventHandler? ResizePreview;

	public event EventHandler? InteractionCanceled;

	public event EventHandler? VisualBoundsChanged;

	public DesignerItem(CanvasElementModel model, FrameworkElement visual)
	{
		Model = model;
		_visual = visual;
		base.Focusable = true;
		base.Background = Brushes.Transparent;
		base.HorizontalContentAlignment = HorizontalAlignment.Stretch;
		base.VerticalContentAlignment = VerticalAlignment.Stretch;
		_root = new Grid
		{
			Background = Brushes.Transparent
		};
		_root.Children.Add(visual);
		_selectionBorder = new Border
		{
			BorderBrush = new SolidColorBrush(Color.FromRgb(43, 182, 200)),
			BorderThickness = new Thickness(1.5),
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
		_root.Children.Add(_selectionBorder);
		CreateResizeHandles();
		CreateRotationHandle();
		base.Content = _root;
		base.PreviewMouseLeftButtonDown += OnMouseDown;
		base.PreviewMouseMove += OnMouseMove;
		base.PreviewMouseLeftButtonUp += OnMouseUp;
	}

	public void ReplaceVisual(FrameworkElement visual)
	{
		if (_root.Children.Count > 0)
		{
			_root.Children.RemoveAt(0);
		}
		_root.Children.Insert(0, visual);
		_visual = visual;
	}

	public void SetExportMode(bool enabled)
	{
		_exportMode = enabled;
		UpdateSelection(IsSelected);
	}

	private void OnMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.Source is Thumb || FindParentThumb(e.OriginalSource as DependencyObject) != null)
		{
			return;
		}
		bool additive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
		this.SelectionRequested?.Invoke(this, new DesignerItemSelectionEventArgs(additive));
		if (Model.IsLocked)
		{
			e.Handled = true;
		}
		else if (base.Parent is Canvas relativeTo)
		{
			_dragStart = e.GetPosition(relativeTo);
			_pressScreen = PointToScreen(e.GetPosition(this));
			_originalLeft = Canvas.GetLeft(this);
			_originalTop = Canvas.GetTop(this);
			if (double.IsNaN(_originalLeft))
			{
				_originalLeft = 0.0;
			}
			if (double.IsNaN(_originalTop))
			{
				_originalTop = 0.0;
			}
			_pointerPressed = true;
			_dragInitiated = false;
			_moving = false;
			CaptureMouse();
			e.Handled = true;
		}
	}

	private void OnMouseMove(object sender, MouseEventArgs e)
	{
		if (!_pointerPressed || e.LeftButton != MouseButtonState.Pressed || !(base.Parent is Canvas relativeTo))
		{
			return;
		}
		if (!_dragInitiated)
		{
			Vector vector = PointToScreen(e.GetPosition(this)) - _pressScreen;
			if (Math.Abs(vector.X) < 4.0 && Math.Abs(vector.Y) < 4.0)
			{
				return;
			}
			_dragInitiated = true;
			_moving = true;
			this.ChangeStarted?.Invoke(this, EventArgs.Empty);
			this.MoveStarted?.Invoke(this, EventArgs.Empty);
		}
		Point position = e.GetPosition(relativeTo);
		Point arg = new Point(_originalLeft + position.X - _dragStart.X, _originalTop + position.Y - _dragStart.Y);
		if (SnapPosition != null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
		{
			arg = SnapPosition(this, arg);
		}
		Canvas.SetLeft(this, arg.X);
		Canvas.SetTop(this, arg.Y);
		this.VisualBoundsChanged?.Invoke(this, EventArgs.Empty);
		this.MovePreview?.Invoke(this, new DesignerItemMoveEventArgs(arg.X - _originalLeft, arg.Y - _originalTop));
		e.Handled = true;
	}

	private void OnMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_moving)
		{
			_moving = false;
			_pointerPressed = false;
			ReleaseMouseCapture();
			Point arg = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
			if (SnapPosition != null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
			{
				arg = SnapPosition(this, arg);
			}
			Canvas.SetLeft(this, arg.X);
			Canvas.SetTop(this, arg.Y);
			this.VisualBoundsChanged?.Invoke(this, EventArgs.Empty);
			this.MovePreview?.Invoke(this, new DesignerItemMoveEventArgs(arg.X - _originalLeft, arg.Y - _originalTop));
			CommitBounds();
			this.MoveFinished?.Invoke(this, EventArgs.Empty);
			this.ModelChanged?.Invoke(this, EventArgs.Empty);
			this.ChangeCompleted?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
		else if (_pointerPressed)
		{
			_pointerPressed = false;
			_dragInitiated = false;
			ReleaseMouseCapture();
			e.Handled = true;
		}
	}

	public bool CancelInteraction()
	{
		bool num = _pointerPressed || _moving || _resizing || _rotating;
		bool flag = _dragInitiated || _resizing || _rotating;
		if (!num)
		{
			return false;
		}
		if (_moving || _dragInitiated)
		{
			Canvas.SetLeft(this, _originalLeft);
			Canvas.SetTop(this, _originalTop);
		}
		if (_resizing)
		{
			Canvas.SetLeft(this, _resizeStartRect.Left);
			Canvas.SetTop(this, _resizeStartRect.Top);
			base.Width = _resizeStartRect.Width;
			base.Height = _resizeStartRect.Height;
			RestoreTextScale();
		}
		if (_rotating)
		{
			_pendingRotation = _rotationStart;
			base.RenderTransformOrigin = new Point(0.5, 0.5);
			base.RenderTransform = new TransformGroup
			{
				Children = 
				{
					(Transform)new SkewTransform(Math.Clamp(Model.SkewX, -80.0, 80.0), Math.Clamp(Model.SkewY, -80.0, 80.0)),
					(Transform)new RotateTransform(_rotationStart)
				}
			};
		}
		_pointerPressed = false;
		_dragInitiated = false;
		_moving = false;
		_resizing = false;
		_rotating = false;
		if (base.IsMouseCaptured)
		{
			ReleaseMouseCapture();
		}
		else if (Mouse.Captured != null)
		{
			Mouse.Capture(null);
		}
		this.VisualBoundsChanged?.Invoke(this, EventArgs.Empty);
		this.InteractionCanceled?.Invoke(this, EventArgs.Empty);
		if (flag)
		{
			this.ChangeCompleted?.Invoke(this, EventArgs.Empty);
		}
		return true;
	}

	private void CreateResizeHandles()
	{
		AddHandle("NW", HorizontalAlignment.Left, VerticalAlignment.Top, Cursors.SizeNWSE);
		AddHandle("N", HorizontalAlignment.Center, VerticalAlignment.Top, Cursors.SizeNS);
		AddHandle("NE", HorizontalAlignment.Right, VerticalAlignment.Top, Cursors.SizeNESW);
		AddHandle("E", HorizontalAlignment.Right, VerticalAlignment.Center, Cursors.SizeWE);
		AddHandle("SE", HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE);
		AddHandle("S", HorizontalAlignment.Center, VerticalAlignment.Bottom, Cursors.SizeNS);
		AddHandle("SW", HorizontalAlignment.Left, VerticalAlignment.Bottom, Cursors.SizeNESW);
		AddHandle("W", HorizontalAlignment.Left, VerticalAlignment.Center, Cursors.SizeWE);
	}

	private void AddHandle(string direction, HorizontalAlignment horizontal, VerticalAlignment vertical, Cursor cursor)
	{
		Thumb thumb = new Thumb
		{
			Tag = direction,
			Width = 9.0,
			Height = 9.0,
			HorizontalAlignment = horizontal,
			VerticalAlignment = vertical,
			Margin = new Thickness(-4.5),
			Cursor = cursor,
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(43, 182, 200)),
			BorderThickness = new Thickness(1.2),
			Visibility = Visibility.Collapsed
		};
		thumb.DragStarted += ResizeDragStarted;
		thumb.DragDelta += ResizeDragDelta;
		thumb.DragCompleted += ResizeDragCompleted;
		_handles.Add(thumb);
		_root.Children.Add(thumb);
	}

	private void ResizeDragDelta(object sender, DragDeltaEventArgs e)
	{
		if (_resizing && !Model.IsLocked && base.Parent is Canvas relativeTo)
		{
			Point position = Mouse.GetPosition(relativeTo);
			double dx = position.X - _resizeStartPointer.X;
			double dy = position.Y - _resizeStartPointer.Y;
			bool proportionalOnly = Model.Kind == ElementKind.QrCode || (Model.Kind == ElementKind.Text && Model.TextFrameTight);
			bool preserveAspect = proportionalOnly || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Model.PreserveAspectRatio;
			double minimumMm = Model.Kind == ElementKind.QrCode ? DimensionMath.QrMinimumMm : DimensionMath.GeneralMinimumMm;
			double minimumWidth = minimumMm * 96.0 / 25.4;
			double minimumHeight = minimumMm * 96.0 / 25.4;
			if (Model.Kind == ElementKind.Text && Model.TextFrameTight)
			{
				double minimumTextScale = (_resizeStartFontSizePt > 0.0) ? Math.Min(1.0, 3.0 / _resizeStartFontSizePt) : 1.0;
				minimumWidth = Math.Max(1.0, _resizeStartRect.Width * minimumTextScale);
				minimumHeight = Math.Max(1.0, _resizeStartRect.Height * minimumTextScale);
			}
			Rect rect = CalculateResizeRect(_resizeStartRect, _resizeDirection, dx, dy, preserveAspect, minimumWidth, minimumHeight);
			if (Model.Kind == ElementKind.Text && Model.TextFrameTight)
			{
				double requestedScale = rect.Width / _resizeStartRect.Width;
				double allowedScale = DimensionMath.ClampTextScale(_resizeStartFontSizePt, requestedScale);
				ResizeBounds constrained = ResizeMath.ApplyUniformScale(new ResizeBounds(_resizeStartRect.X, _resizeStartRect.Y, _resizeStartRect.Width, _resizeStartRect.Height), _resizeDirection, allowedScale);
				rect = new Rect(constrained.X, constrained.Y, constrained.Width, constrained.Height);
			}
			Canvas.SetLeft(this, rect.Left);
			Canvas.SetTop(this, rect.Top);
			base.Width = rect.Width;
			base.Height = rect.Height;
			ApplyTextScale(rect);
			CommitBounds();
			this.VisualBoundsChanged?.Invoke(this, EventArgs.Empty);
			this.ResizePreview?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
	}

	private void ResizeDragStarted(object sender, DragStartedEventArgs e)
	{
		if (!Model.IsLocked && sender is Thumb thumb && base.Parent is Canvas relativeTo)
		{
			_resizeDirection = thumb.Tag?.ToString() ?? string.Empty;
			double num = Canvas.GetLeft(this);
			if (double.IsNaN(num))
			{
				num = 0.0;
			}
			double num2 = Canvas.GetTop(this);
			if (double.IsNaN(num2))
			{
				num2 = 0.0;
			}
			double minimumStartSize = (Model.Kind == ElementKind.Text && Model.TextFrameTight) ? 1.0 : ((Model.Kind == ElementKind.QrCode ? DimensionMath.QrMinimumMm : DimensionMath.GeneralMinimumMm) * 96.0 / 25.4);
			_resizeStartRect = new Rect(num, num2, Math.Max(minimumStartSize, base.ActualWidth), Math.Max(minimumStartSize, base.ActualHeight));
			_resizeStartFontSizePt = Model.FontSizePt;
			_resizeStartCharacterSpacing = Model.CharacterSpacing;
			_resizeStartLineSpacingPt = Model.LineSpacingPt;
			_resizeStartLineHeight = Model.LineHeight;
			_resizeStartOutlineThicknessPt = Model.TextOutlineThicknessPt;
			_resizeStartExtrusionDepthPt = Model.TextExtrusionDepthPt;
			_resizeStartPointer = Mouse.GetPosition(relativeTo);
			_resizing = true;
			thumb.CaptureMouse();
			this.ChangeStarted?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ApplyTextScale(Rect rect)
	{
		if (Model.Kind != ElementKind.Text || !Model.TextFrameTight || _resizeStartRect.Width <= 0.0)
		{
			return;
		}
		double scaleX = Math.Max(0.05, rect.Width / _resizeStartRect.Width);
		double scaleY = Math.Max(0.05, rect.Height / _resizeStartRect.Height);
		double scale;
		if ((_resizeDirection.Contains('E') || _resizeDirection.Contains('W')) && (_resizeDirection.Contains('N') || _resizeDirection.Contains('S')))
		{
			scale = Math.Sqrt(scaleX * scaleY);
		}
		else
		{
			scale = (_resizeDirection.Contains('E') || _resizeDirection.Contains('W')) ? scaleX : scaleY;
		}
		Model.FontSizePt = Math.Clamp(_resizeStartFontSizePt * scale, 3.0, 300.0);
		Model.CharacterSpacing = Math.Clamp(_resizeStartCharacterSpacing * scale, -100.0, 300.0);
		Model.LineSpacingPt = Math.Clamp(_resizeStartLineSpacingPt * scale, -100.0, 300.0);
		Model.LineHeight = Math.Max(0.0, _resizeStartLineHeight * scale);
		Model.TextOutlineThicknessPt = Math.Clamp(_resizeStartOutlineThicknessPt * scale, 0.0, 24.0);
		Model.TextExtrusionDepthPt = Math.Clamp(_resizeStartExtrusionDepthPt * scale, 0.0, 48.0);
		_visual.InvalidateVisual();
	}

	private void RestoreTextScale()
	{
		if (Model.Kind != ElementKind.Text || !Model.TextFrameTight)
		{
			return;
		}
		Model.FontSizePt = _resizeStartFontSizePt;
		Model.CharacterSpacing = _resizeStartCharacterSpacing;
		Model.LineSpacingPt = _resizeStartLineSpacingPt;
		Model.LineHeight = _resizeStartLineHeight;
		Model.TextOutlineThicknessPt = _resizeStartOutlineThicknessPt;
		Model.TextExtrusionDepthPt = _resizeStartExtrusionDepthPt;
		_visual.InvalidateVisual();
	}

	private void ResizeDragCompleted(object sender, DragCompletedEventArgs e)
	{
		if (_resizing)
		{
			_resizing = false;
			if (sender is Thumb thumb && thumb.IsMouseCaptured)
			{
				thumb.ReleaseMouseCapture();
			}
			CommitBounds();
			this.ModelChanged?.Invoke(this, EventArgs.Empty);
			this.ChangeCompleted?.Invoke(this, EventArgs.Empty);
		}
	}

	private static Rect CalculateResizeRect(Rect start, string direction, double dx, double dy, bool preserveAspect, double minimumWidth, double minimumHeight)
	{
		ResizeBounds result = ResizeMath.Calculate(new ResizeBounds(start.X, start.Y, start.Width, start.Height), direction, dx, dy, preserveAspect, minimumWidth, minimumHeight);
		return new Rect(result.X, result.Y, result.Width, result.Height);
	}

	private void CommitBounds()
	{
		double num = Canvas.GetLeft(this);
		if (double.IsNaN(num))
		{
			num = 0.0;
		}
		double num2 = Canvas.GetTop(this);
		if (double.IsNaN(num2))
		{
			num2 = 0.0;
		}
		Model.Xmm = num * 25.4 / 96.0;
		Model.Ymm = num2 * 25.4 / 96.0;
		double val = (double.IsNaN(base.Width) ? base.ActualWidth : base.Width);
		double val2 = (double.IsNaN(base.Height) ? base.ActualHeight : base.Height);
		double minimumCommittedSize = (Model.Kind == ElementKind.Text && Model.TextFrameTight) ? 1.0 : ((Model.Kind == ElementKind.QrCode ? DimensionMath.QrMinimumMm : DimensionMath.GeneralMinimumMm) * 96.0 / 25.4);
		Model.WidthMm = Math.Max(minimumCommittedSize, val) * 25.4 / 96.0;
		Model.HeightMm = Math.Max(minimumCommittedSize, val2) * 25.4 / 96.0;
	}

	private void CreateRotationHandle()
	{
		Thumb thumb = new Thumb
		{
			Tag = "Rotate",
			Width = 12.0,
			Height = 12.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, -28.0, 0.0, 0.0),
			Cursor = Cursors.Hand,
			Background = new SolidColorBrush(Color.FromRgb(242, 106, 33)),
			BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0),
			Visibility = Visibility.Collapsed,
			ToolTip = "ドラッグで45°ごとに回転／Shiftを押しながら自由回転"
		};
		thumb.DragStarted += delegate
		{
			_rotationStart = Model.Rotation;
			_pendingRotation = _rotationStart;
			_rotating = true;
			this.ChangeStarted?.Invoke(this, EventArgs.Empty);
		};
		thumb.DragDelta += delegate
		{
			if (!Model.IsLocked && base.Parent is Canvas relativeTo)
			{
				Point point = new Point(Canvas.GetLeft(this) + base.ActualWidth / 2.0, Canvas.GetTop(this) + base.ActualHeight / 2.0);
				Point position = Mouse.GetPosition(relativeTo);
				double num = Math.Atan2(position.Y - point.Y, position.X - point.X) * 180.0 / Math.PI + 90.0;
				if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
				{
					num = Math.Round(num / 45.0, MidpointRounding.AwayFromZero) * 45.0;
				}
				num = (_pendingRotation = (num % 360.0 + 360.0) % 360.0);
				base.RenderTransformOrigin = new Point(0.5, 0.5);
				base.RenderTransform = new TransformGroup
				{
					Children = 
					{
						(Transform)new SkewTransform(Math.Clamp(Model.SkewX, -80.0, 80.0), Math.Clamp(Model.SkewY, -80.0, 80.0)),
						(Transform)new RotateTransform(num)
					}
				};
				this.VisualBoundsChanged?.Invoke(this, EventArgs.Empty);
			}
		};
		thumb.DragCompleted += delegate
		{
			_rotating = false;
			Model.Rotation = _pendingRotation;
			this.ModelChanged?.Invoke(this, EventArgs.Empty);
			this.ChangeCompleted?.Invoke(this, EventArgs.Empty);
		};
		_handles.Add(thumb);
		_root.Children.Add(thumb);
		UpdateHandleMetrics();
	}

	private void UpdateHandleMetrics()
	{
		HandleMetrics metrics = ZoomHandleMath.Calculate(_displayZoom);
		_selectionBorder.BorderThickness = new Thickness(metrics.SelectionBorder);
		foreach (Thumb handle in _handles)
		{
			bool rotation = string.Equals(handle.Tag?.ToString(), "Rotate", StringComparison.Ordinal);
			if (rotation)
			{
				handle.Width = metrics.RotationSize;
				handle.Height = metrics.RotationSize;
				handle.Margin = new Thickness(0.0, metrics.RotationOffset, 0.0, 0.0);
				handle.BorderThickness = new Thickness(metrics.RotationBorder);
			}
			else
			{
				handle.Width = metrics.ResizeSize;
				handle.Height = metrics.ResizeSize;
				handle.Margin = new Thickness(metrics.ResizeOffset);
				handle.BorderThickness = new Thickness(metrics.ResizeBorder);
			}
		}
	}

	private void UpdateSelection(bool selected)
	{
		bool flag = selected && !_exportMode && Model.IsVisible;
		_selectionBorder.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		foreach (Thumb handle in _handles)
		{
			handle.Visibility = ((!flag || Model.IsLocked) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private static Thumb? FindParentThumb(DependencyObject? value)
	{
		while (value != null)
		{
			if (value is Thumb result)
			{
				return result;
			}
			try
			{
				value = VisualTreeHelper.GetParent(value);
			}
			catch
			{
				return null;
			}
		}
		return null;
	}
}
