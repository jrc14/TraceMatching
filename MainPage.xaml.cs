namespace TraceMatching;

using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Runtime.CompilerServices;
using TraceMatching.Model;

public partial class MainPage : ContentPage
{

    public MainPage()
    {
        InitializeComponent();

        this.BindingContext = this;

        this.lyCanvasView.Children.Clear();

        SKCanvasView view = new SKCanvasView()
        {
            HeightRequest=300,
            WidthRequest=300
        };

        this.lyCanvasView.Children.Add(view);
        view.EnableTouchEvents = true;
        view.InputTransparent = false;
        view.Touch += SkiaCanvasView_Touch;
        view.PaintSurface += OnCanvasViewPaintSurface;

        this.PropertyChanged += MainPage_PropertyChanged;

        this.SolutionDescription = "no solution found yet";
        this.SolveStatus = "READY";
    }

    private void MainPage_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName== "TracesForTargets")
        {
            ClearSolutionDisplay();
        }
        else if (e.PropertyName== "SolveStatus")
        {
            if(this.SolveStatus=="READY")
            {
                this.lyCanvasView.Opacity = 1.0;
                this.btnClear.Opacity = 1.0;
                this.btnSolve.Opacity = 1.0;
                this.btnStop.Opacity = 0.5;
                this.traceButtons.Opacity = 1.0;
                this.targetButtons.Opacity = 1.0;
            }
            else if(this.SolveStatus=="RUNNING")
            {
                this.lyCanvasView.Opacity = 0.5;
                this.btnClear.Opacity = 0.5;
                this.btnSolve.Opacity = 0.5;
                this.btnStop.Opacity = 1.0;
                this.traceButtons.Opacity = 0.5;
                this.targetButtons.Opacity = 0.5;
            }
        }
    }

    private void OnButtonsChanged(System.Object sender, Microsoft.Maui.Controls.ElementEventArgs e)
    {
        ClearSolutionDisplay();
    }

    private void ClearSolutionDisplay()
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(ClearSolutionDisplay);
        else
        {
            this.SolutionDescription = "no solution found yet";
            if (this.lyCanvasView.Children.Count > 0)
                (this.lyCanvasView.Children[0] as SKCanvasView).InvalidateSurface();
        }
    }

    private void OnSolveClicked(object sender, EventArgs e)
    {
        if(this.SolveStatus!="READY")
        {
            DisplayAlert("Not Ready", "Not ready - press the stop button to interrupt the solver", "OK");
            return;
        }
        this.SolveStatus = "RUNNING";
        this.SolutionDescription = "no solution found yet";

        var targets = new List<Tuple<int, int>>();
        foreach (Button b in this.targetButtons.Children)
        {
            targets.Add(b.CommandParameter as Tuple<int, int>);
        }

        var traces = new List<Tuple<int, int>>();
        foreach (Button b in this.traceButtons.Children)
        {
            traces.Add(b.CommandParameter as Tuple<int, int>);
        }

        Task.Run(() => {
            TraceModel.HaltAfterNextStep = false;
            TraceModel.Solve(targets, traces, DisplaySolution);
            Dispatcher.Dispatch(() => {
                this.SolveStatus = "READY";
            });
        });
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        if (this.SolveStatus != "RUNNING")
        {
            DisplayAlert("Not Running", "Solver is not running; can't stop it", "OK");
            return;
        }

        TraceModel.HaltAfterNextStep = true;
    }

    private void DisplaySolution(int[] solution, string msg, bool isFinal)
    {
        Dispatcher.Dispatch(() =>
        {
            List<Tuple<int, int>> newSolution = new List<Tuple<int, int>>();
            for (int i = 0; i < solution.Length; i++)
            {
                int j = solution[i]; // target #i has been identified with trace #j

                if (j == -1)   // target #i was matched with trace -1, which is to say it was not matched
                    continue;

                newSolution.Add(new Tuple<int, int>(j, i)); // Item1 is a trace number, Item2 is a target number
            }
            this.TracesForTargets = newSolution;
            this.SolutionDescription = msg;
        });
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        if (this.SolveStatus != "READY")
        {
            DisplayAlert("Not Ready", "Not ready - press the stop button to interrupt the solver", "OK");
            return;
        }

        this.SolutionDescription = "no solution found yet";
        this.traceButtons.Children.Clear();
        this.targetButtons.Children.Clear();
        if (this.TracesForTargets != null)
        {
            this.TracesForTargets.Clear();
            this.TracesForTargets = null;
        }
    }

    private void SkiaCanvasTap(int x, int y, bool isRightOrHold)
    {
        if (this.SolveStatus != "READY")
        {
            DisplayAlert("Not Ready", "Not ready - press the stop button to interrupt the solver", "OK");
            return;
        }

        Button btn = new Button()
        {
            WidthRequest = 100,
            HeightRequest = 40,
            TextColor = Colors.White,
            CommandParameter = new Tuple<int, int>(x, y),
            Text = "(" + x.ToString() + ", " + y.ToString() + ")",
            FontSize = 10
        };
        btn.Clicked += (object sender, EventArgs e) =>
        {
            if (this.SolveStatus != "READY")
            {
                DisplayAlert("Not Ready", "Not ready - press the stop button to interrupt the solver", "OK");
                return;
            }

            ((sender as Button).Parent as VerticalStackLayout).Children.Remove((sender as Button));
            if (this.TracesForTargets != null)
            {
                this.TracesForTargets.Clear();
                this.TracesForTargets = null;
            }
        };

        if (isRightOrHold)
        {
            btn.BackgroundColor = Colors.OrangeRed;
            this.traceButtons.Add(btn);
        }
        else
        {
            btn.BackgroundColor = Colors.Blue;
            this.targetButtons.Add(btn);
        }
        if (this.TracesForTargets != null)
        {
            this.TracesForTargets.Clear();
            this.TracesForTargets = null;
        }
    }

    private static int MOVE_THRESHOLD = 5;

    private bool panDetected = false;

    private double priorX = -1;
    private double priorY = -1;
    private double initialX = -1;
    private double initialY = -1;
    private double initialMilliseconds = -1;
    private double priorMilliseconds = -1;

    private void SkiaCanvasView_Touch(object sender, SKTouchEventArgs args)
    {
        try
        {
            args.Handled = true; // Let the OS know that we want to receive more touch events, and that we don't want to pass the event to any other element

            double x = args.Location.X;
            double y = args.Location.Y;

            if (args.ActionType == SKTouchAction.Pressed)
            {
                panDetected = false;

                this.initialX = x;
                this.initialY = y;
                this.priorX = x;
                this.priorY = y;
                this.initialMilliseconds = DateTime.Now.Subtract(DateTime.MinValue).TotalMilliseconds;
                this.priorMilliseconds = this.initialMilliseconds;

                // raise 'pointer pressed' here

                this.Dispatcher.Dispatch(async () =>
                {
                    double startedAtMilliseconds = this.initialMilliseconds;

                    await Task.Delay(500);
                    if (this.initialMilliseconds != -1)
                    {
                        if (this.initialMilliseconds == startedAtMilliseconds && !panDetected)
                        {
                                // raise 'holding' here
                                SkiaCanvasTap((int)this.initialX, (int)this.initialY, true);
                        }
                    }
                });
            }
            else if (args.ActionType == SKTouchAction.Released && this.initialMilliseconds != -1)
            {
                double deltaX = x - this.priorX;
                double deltaY = y - this.priorY;
                double totalX = x - this.initialX;
                double totalY = y - this.initialY;

                double millisecondsNow = DateTime.Now.Subtract(DateTime.MinValue).TotalMilliseconds;

                double totalMilliseconds = millisecondsNow - this.initialMilliseconds;

                this.initialMilliseconds = -1;

                if (panDetected || Math.Abs(totalX) > MOVE_THRESHOLD || Math.Abs(totalY) > MOVE_THRESHOLD)
                {
                    // raise 'pan completed' here
                }
                else
                {
                    // raise 'cancel move' here
                    if (totalMilliseconds > 500)
                    {
                        // raise 'finished holding' here
                    }
                    else
                    {
                        if (args.MouseButton == SKMouseButton.Right)
                        {
                            // raise 'right clicked' here
                            SkiaCanvasTap((int)x, (int)y, true);
                        }
                        else
                        {
                            // raise 'left clicked' here
                            SkiaCanvasTap((int)x, (int)y, false);
                        }
                    }
                }

                // raised 'pointer released' here

                panDetected = false;
            }
            else if (args.ActionType == SKTouchAction.Moved && args.InContact && this.initialMilliseconds != -1)
            {
                double milliseconds = DateTime.Now.Subtract(DateTime.MinValue).TotalMilliseconds;
                double deltaX = x - this.priorX;
                double deltaY = y - this.priorY;
                double deltaMilliseconds = milliseconds - this.priorMilliseconds;
                double totalX = x - this.initialX;
                double totalY = y - this.initialY;
                double totalMilliseconds = milliseconds - this.initialMilliseconds;
                this.priorX = x;
                this.priorY = y;
                this.priorMilliseconds = milliseconds;

                // raise 'panning' here

                if (Math.Abs(totalX) >= MOVE_THRESHOLD || Math.Abs(totalY) >= MOVE_THRESHOLD
                || Math.Abs(deltaX) >= MOVE_THRESHOLD || Math.Abs(deltaY) >= MOVE_THRESHOLD)
                {
                    this.panDetected = true;
                }
            }
            else if (args.ActionType == SKTouchAction.Cancelled)
            {
                this.initialMilliseconds = -1;

                // raise 'pan cancelled' here
            }
        }
        catch (Exception ex)
        {

        }
    }

    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
    {
        args.Surface.Canvas.Clear(SkiaSharp.SKColors.DarkGray);

        if (this.TracesForTargets != null)
        {
            foreach (Tuple<int, int> t in this.TracesForTargets)
            {
                int i = t.Item2; // target number
                int j = t.Item1; // trace number

                Button b1 = this.targetButtons.Children[i] as Button;
                int x1 = (b1.CommandParameter as Tuple<int, int>).Item1;
                int y1 = (b1.CommandParameter as Tuple<int, int>).Item2;
                SKPoint pt1 = new SKPoint(x1, y1);

                Button b2 = this.traceButtons.Children[j] as Button;
                int x2 = (b2.CommandParameter as Tuple<int, int>).Item1;
                int y2 = (b2.CommandParameter as Tuple<int, int>).Item2;
                SKPoint pt2 = new SKPoint(x2, y2);

                args.Surface.Canvas.DrawLine(pt1, pt2, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.Orange, Style = SkiaSharp.SKPaintStyle.StrokeAndFill, StrokeWidth = 5 }); ;
            }
        }

        for(int i=0; i<this.targetButtons.Count; i++)
        {
            Button b = this.targetButtons.Children[i] as Button;
            int x = (b.CommandParameter as Tuple<int, int>).Item1;
            int y = (b.CommandParameter as Tuple<int, int>).Item2;

            args.Surface.Canvas.DrawCircle(x, y, 20, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.Blue });
            args.Surface.Canvas.DrawText(i.ToString(), x-5, y+5, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.White, TextSize = 20 });

            if (TracesForTargets != null)
            {
                if (!TracesForTargets.Any(x => x.Item2 == i)) // i is a target that we didn't match to any trace
                {
                    args.Surface.Canvas.DrawCircle(x, y, 25, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.Red, Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 5 });
                }
            }
        }

        for (int j = 0; j < this.traceButtons.Count; j++)
        {
            Button b = this.traceButtons.Children[j] as Button;
            int x = (b.CommandParameter as Tuple<int, int>).Item1;
            int y = (b.CommandParameter as Tuple<int, int>).Item2;

            args.Surface.Canvas.DrawCircle(x, y, 20, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.OrangeRed });
            args.Surface.Canvas.DrawText(j.ToString(), x-5, y+5, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.White, TextSize=20 });

            if (this.TracesForTargets != null)
            {
                if (!this.TracesForTargets.Any(x => x.Item1 == j)) // j is a trace that we didn't match to any target
                {
                    args.Surface.Canvas.DrawCircle(x, y, 25, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.Red, Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 5 });
                }
            }
        }
    }

    private string _SolveStatus = null;
    public string SolveStatus
    {
        get
        {
            return _SolveStatus;
        }
        set
        {
            _SolveStatus = value;

            this.OnPropertyChanged("SolveStatus");            
        }
    }

    private string _SolutionDescription = null;
    public string SolutionDescription
    {
        get
        {
            return _SolutionDescription;
        }
        set
        {
            _SolutionDescription = value;

            this.OnPropertyChanged("SolutionDescription");
        }
    }

    private List<Tuple<int, int>> _TracesForTargets = null; // in each tuple Item1 is a trace number, Item2 is a target number

    /// <summary>
    /// A list of tuples desribing a potential solution; Item1 is a trace number, Item2 is a target number.
    /// </summary>
    public List<Tuple<int, int>> TracesForTargets
    {
        get
        {
            return _TracesForTargets;
        }
        set
        {
            _TracesForTargets = value;

            this.OnPropertyChanged("TracesForTargets");
        }
    }
}

