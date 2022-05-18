namespace Nop.Plugin.ExternalAuth.OAuth.Models;

public class ExhibitorSelectorModel
{
    public ExhibitorModel[] Exhibitors { get;set; }
    public string SelectedExhibitorId { get; set; }
}