namespace Guard.Components.Library.Dialogs;

public sealed record EntityDialogResult(bool Saved, object? EntityId = null)
{
  public static bool IsSuccess(object? value) => value is EntityDialogResult { Saved: true } || value is true;
}
