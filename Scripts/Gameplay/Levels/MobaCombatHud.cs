using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Gameplay.Levels;

/// <summary>Screen-space health, floating resolved outcomes and collapsible history; no gameplay mutations.</summary>
public partial class MobaCombatHud : CanvasLayer
{
    private sealed record HealthWidget(PanelContainer Root,ProgressBar Bar,Label Text,Line2D Leader);
    private sealed record Popup(MobaCombatEvent Event,double StartsAt,Label Label);
    private readonly Dictionary<string,HealthWidget> _health=new();
    private readonly List<Popup> _popups=new();
    private IReadOnlyList<StaticTacticalCharacter> _fixtures=null!;
    private Camera3D _camera=null!;
    private Button _historyToggle=null!;
    private RichTextLabel _history=null!;
    private double _time,_nextPopup;
    private int _recordCount=-1;
    public int FloatingCount=>_popups.Count;
    public bool HistoryExpanded=>_history.Visible;
    public string HistoryText=>_history.Text;
    public bool HealthVisible(string id)=>_health[id].Root.Visible;
    public string HealthText(string id)=>_health[id].Text.Text;

    public void Initialize(IReadOnlyList<StaticTacticalCharacter> fixtures,Camera3D camera)
    {
        _fixtures=fixtures;_camera=camera;
        foreach(var fixture in fixtures)
        {
            var leader=new Line2D {Width=1,DefaultColor=fixture.TeamId==1?new(.3f,.75f,1,.8f):new(1,.4f,.4f,.8f),Visible=false};AddChild(leader);
            var panel=new PanelContainer {Name="Health_"+fixture.CharacterId,MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(panel);
            var box=new VBoxContainer {MouseFilter=Control.MouseFilterEnum.Ignore};panel.AddChild(box);box.AddThemeConstantOverride("separation",1);
            var text=new Label {HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};
            text.AddThemeFontSizeOverride("font_size",12);box.AddChild(text);
            var bar=new ProgressBar {MinValue=0,MaxValue=1,ShowPercentage=false,CustomMinimumSize=new(70,6),MouseFilter=Control.MouseFilterEnum.Ignore};
            var fill=new StyleBoxFlat {BgColor=fixture.TeamId==1?new(.1f,.7f,1):new(1,.2f,.28f)};
            bar.AddThemeStyleboxOverride("fill",fill);box.AddChild(bar);_health.Add(fixture.CharacterId,new(panel,bar,text,leader));
        }
        var historyPanel=new PanelContainer {Name="CombatHistory",AnchorLeft=1,AnchorRight=1,OffsetLeft=-450,OffsetRight=-14,OffsetTop=184};AddChild(historyPanel);
        var column=new VBoxContainer();historyPanel.AddChild(column);
        _historyToggle=new Button {Text="Combat history (0) ▸",ToggleMode=true,FocusMode=Control.FocusModeEnum.None};column.AddChild(_historyToggle);
        _history=new RichTextLabel {Visible=false,CustomMinimumSize=new(420,190),ScrollActive=true,ScrollFollowing=true,BbcodeEnabled=false};
        _history.AddThemeFontSizeOverride("normal_font_size",14);column.AddChild(_history);
        _historyToggle.Toggled+=expanded=>{_history.Visible=expanded;UpdateHistoryTitle();};
    }
    public void ToggleHistory()=>_historyToggle.ButtonPressed=!_historyToggle.ButtonPressed;
    private void UpdateHistoryTitle()=>_historyToggle.Text=$"Combat history ({Math.Max(0,_recordCount)}) {(_history.Visible?"▾":"▸")}";
    public void Refresh(MobaHotseatSession session)
    {
        if(_recordCount!=session.CombatHistory.Records.Count)
        {_recordCount=session.CombatHistory.Records.Count;_history.Text=session.CombatHistory.DisplayText();UpdateHistoryTitle();}
        var placed=new List<Rect2>();
        foreach(var fixture in _fixtures.OrderBy(f=>f.CharacterId,StringComparer.Ordinal))
        {
            var state=fixture.Presentation!;var widget=_health[fixture.CharacterId];
            widget.Root.Visible=state.ShowHealthBar&&fixture.VisibleToObserver&&!_camera.IsPositionBehind(fixture.GlobalPosition);
            widget.Text.Text=state.HealthText.Replace(" · ","\n");widget.Bar.Value=state.HealthFraction;
            widget.Root.ResetSize(); // Shrink again when the shield line disappears, including DEV reset.
            var anchor=_camera.UnprojectPosition(fixture.GlobalPosition+new Vector3(0,2.1f,0));
            var desired=anchor-new Vector2(widget.Root.Size.X/2,widget.Root.Size.Y);var position=desired;
            if(widget.Root.Visible)
            {
                while(placed.Any(r=>r.Grow(2).Intersects(new Rect2(position,widget.Root.Size))))position.Y-=widget.Root.Size.Y+5;
                placed.Add(new(position,widget.Root.Size));
            }
            widget.Root.Position=position;widget.Leader.Visible=widget.Root.Visible&&position.DistanceTo(desired)>1;
            widget.Leader.Points=new[]{position+new Vector2(widget.Root.Size.X/2,widget.Root.Size.Y),anchor};
        }
    }
    public void Present(MobaCombatRecord record)
    {
        _nextPopup=Math.Max(_time,_nextPopup);
        foreach(var entry in record.Events)
        {
            var label=new Label {Text=entry.FloatingText,Visible=false,MouseFilter=Control.MouseFilterEnum.Ignore,HorizontalAlignment=HorizontalAlignment.Center};
            label.AddThemeFontSizeOverride("font_size",18);
            label.AddThemeColorOverride("font_color",entry.Kind==MobaFeedbackKind.Defeat?new Color(1,.25f,.25f):entry.Kind==MobaFeedbackKind.Shield?new Color(.3f,.85f,1):entry.Hit==false?Colors.LightGray:Colors.Gold);
            label.AddThemeColorOverride("font_outline_color",Colors.Black);label.AddThemeConstantOverride("outline_size",5);
            AddChild(label);_popups.Add(new(entry,_nextPopup,label));_nextPopup+=.9;
        }
    }
    public override void _Process(double delta)
    {
        _time+=Math.Max(0,delta);
        foreach(var popup in _popups.ToArray())
        {
            double age=_time-popup.StartsAt;
            if(age<0)continue;
            if(age>1.5){popup.Label.QueueFree();_popups.Remove(popup);continue;}
            var view=_fixtures.Single(f=>f.CharacterId==popup.Event.TargetId);
            popup.Label.Visible=!_camera.IsPositionBehind(view.GlobalPosition);
            popup.Label.Position=_camera.UnprojectPosition(view.GlobalPosition+new Vector3(0,3.8f,0))
                -new Vector2(popup.Label.Size.X/2,(float)age*28);
            popup.Label.Modulate=new Color(1,1,1,(float)Math.Min(1,(1.5-age)*2));
        }
    }
    public void Clear()
    {
        foreach(var popup in _popups){RemoveChild(popup.Label);popup.Label.QueueFree();}
        _popups.Clear();_nextPopup=_time;_recordCount=-1;_history.Text="";_historyToggle.ButtonPressed=false;
    }
}
