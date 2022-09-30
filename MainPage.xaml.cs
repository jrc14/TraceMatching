namespace TraceMatching;

using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

public partial class MainPage : ContentPage
{

    public MainPage()
    {
        InitializeComponent();

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
    }

    private void OnSolveClicked(object sender, EventArgs e)
    {
        var targets = new List<Tuple<int,int >> ();
        foreach(Button b in this.targetButtons.Children)
        {
            targets.Add(b.CommandParameter as Tuple<int, int>);
        }

        var traces = new List<Tuple<int, int>>();
        foreach (Button b in this.traceButtons.Children)
        {
            traces.Add(b.CommandParameter as Tuple<int, int>);
        }

        Model.TraceModel.Solve(targets, traces, DisplaySolution);
    }

    List<Tuple<int, int>> traceMatches = null;
    private void DisplaySolution(int[] solution, string msg)
    {
        this.traceMatches= new List<Tuple<int, int>>();
        for(int i=0; i<solution.Length; i++)
        {
            int j = solution[i]; // target #i has been identified with trace #j

            if (j == -1)   // target #i was matched with trace -1, which is to say it was not matched
                continue;

            traceMatches.Add(new Tuple<int, int>(j, i)); // Item1 is a trace number, Item2 is a target number
        }
        (this.lyCanvasView.Children[0] as SKCanvasView).InvalidateSurface();
        this.lblResult.Text = msg;
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        this.traceButtons.Children.Clear();
        this.targetButtons.Children.Clear();
        if (this.traceMatches != null)
        {
            this.traceMatches.Clear();
            this.traceMatches = null;
        }
        (this.lyCanvasView.Children[0] as SKCanvasView).InvalidateSurface();
    }

    private void SkiaCanvasView_Touch(object sender, SKTouchEventArgs args)
    {
        if (args.ActionType == SKTouchAction.Pressed)
        {
            int x = (int)args.Location.X;
            int y = (int)args.Location.Y;

            Button btn = new Button()
            {
                WidthRequest=100,
                HeightRequest=40,
                TextColor=Colors.White,
                CommandParameter=new Tuple<int,int>(x,y),
                Text="("+x.ToString()+", "+y.ToString()+")",
                FontSize=10
            };
            btn.Clicked += (object sender, EventArgs e) =>
            {
                this.Dispatcher.Dispatch(() =>
                {
                    if (this.traceMatches != null)
                    {
                        this.traceMatches.Clear();
                        this.traceMatches = null;
                    }
                    ((sender as Button).Parent as VerticalStackLayout).Children.Remove((sender as Button));
                    (this.lyCanvasView.Children[0] as SKCanvasView).InvalidateSurface();
                });
            };

            if (this.traceMatches != null)
            {
                this.traceMatches.Clear();
                this.traceMatches = null;
            }

            if (args.MouseButton == SKMouseButton.Right)
            {
                btn.BackgroundColor = Colors.OrangeRed;
                this.traceButtons.Add(btn);
                (this.lyCanvasView.Children[0] as SKCanvasView).InvalidateSurface();
            }
            else
            {
                btn.BackgroundColor = Colors.Blue;
                this.targetButtons.Add(btn);
                (this.lyCanvasView.Children[0] as SKCanvasView).InvalidateSurface();
            }
        }
    }

    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
    {
        args.Surface.Canvas.Clear(SkiaSharp.SKColors.DarkGray);

        if (this.traceMatches != null)
        {
            foreach (Tuple<int, int> t in this.traceMatches)
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

            if (traceMatches!=null)
            {
                if (!traceMatches.Any(x => x.Item2 == i)) // i is a target that we didn't match to any trace
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

            if (traceMatches!=null)
            {
                if (!traceMatches.Any(x => x.Item1 == j)) // j is a trace that we didn't match to any target
                {
                    args.Surface.Canvas.DrawCircle(x, y, 25, new SkiaSharp.SKPaint() { Color = SkiaSharp.SKColors.Red, Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 5 });
                }
            }
        }
    }
}

