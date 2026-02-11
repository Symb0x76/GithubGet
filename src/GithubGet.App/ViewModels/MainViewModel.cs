namespace GithubGet.App.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private string _appTitle = "GithubGet";

    public MainViewModel()
    {
        Title = "GithubGet";
    }

    public string AppTitle
    {
        get => _appTitle;
        set => SetProperty(ref _appTitle, value);
    }
}
