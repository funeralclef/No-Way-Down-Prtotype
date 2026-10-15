using Godot;

/// <summary>Keeps the prototype's interface in English on every system locale.</summary>
public partial class GameLocale : Node
{
    public override void _EnterTree() => TranslationServer.SetLocale("en");
}
