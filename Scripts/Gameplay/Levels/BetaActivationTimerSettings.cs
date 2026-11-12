using Godot;
using PawnsOfDivinity.Gameplay.Levels;

[Tool,GlobalClass]
public partial class BetaActivationTimerSettings : Resource
{
    [Export] public bool Enabled {get;set;}=true;
    [Export(PropertyHint.Range,"0.01,3600,0.01,or_greater")] public double DurationSeconds {get;set;}=30;
    public ActivationTimerConfiguration Snapshot(){var c=new ActivationTimerConfiguration(Enabled,DurationSeconds);c.Validate();return c;}
}
