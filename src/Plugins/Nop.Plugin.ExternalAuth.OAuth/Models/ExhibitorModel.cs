using System;

namespace Nop.Plugin.ExternalAuth.OAuth.Models;

public class ExhibitorModel
{
    public string Event { get; set; }
    public string Exhibitor { get; set; }
    public string ExhibitorId { get; set; }

    public bool IdEquals(string exhibitorId)
    {
        return ExhibitorId.Equals(exhibitorId, StringComparison.OrdinalIgnoreCase);
    }
}