namespace TraceMatching;

public partial class MainPage : ContentPage
{

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnSolveClicked(object sender, EventArgs e)
	{
		Model.TraceModel.Solve((string s)=>this.lblResult.Text=s);
	}
}

