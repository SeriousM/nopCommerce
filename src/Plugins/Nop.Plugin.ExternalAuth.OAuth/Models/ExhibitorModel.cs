using System;

namespace Nop.Plugin.ExternalAuth.OAuth.Models;

public class ExhibitorModel
{
    public string EventId { get; set; }
    public string ExhibitorName { get; set; }
    public string ExhibitorId { get; set; }
    public string EventName { get; set; }
    public int CompanyId { get; set; }
    public string Hall { get; set; }
    public string Booth { get; set; }

    public bool IdEquals(string exhibitorId)
    {
        return ExhibitorId.Equals(exhibitorId, StringComparison.OrdinalIgnoreCase);
    }
}