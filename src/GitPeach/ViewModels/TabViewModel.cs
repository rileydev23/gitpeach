using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitPeach.ViewModels;

public abstract partial class TabViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    public virtual bool Closable => true;
    public ICommand? CloseCommand { get; set; }
}
