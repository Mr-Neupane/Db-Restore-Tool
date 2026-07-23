namespace DbRestoreTool.Model;

public class RestoreSetting
{
   public bool IsPostgres { get; set; }
   public bool SelectionForServer { get; set; }
   public string RawQueryFilePath { get; set; }
   public bool DbRenameOption { get; set; }


}