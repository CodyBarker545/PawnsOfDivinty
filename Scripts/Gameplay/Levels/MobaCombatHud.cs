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
    private PanelContainer _historyPanel=null!;
    private MobaTacticalHud _hud=null!;
    private double _time,_nextPopup;
    private int _recordCount=-1;
    public int FloatingCount=>_popups.Count;
    public bool HistoryExpanded=>_history.Visible;
    public string HistoryText=>_history.Text;
    public bool HealthVisible(string id)=>_health.TryGetValue(id,out var widget)&&widget.Root.Visible;
    public string HealthText(string id)=>_health[id].Text.Text;
    public bool CoversBattlefieldPoint(Vector2 point)=>_historyPanel.Visible&&_historyPanel.GetGlobalRect().HasPoint(point);

    public void Initialize(IReadOnlyList<StaticTacticalCharacter> fixtures,Camera3D camera,MobaTacticalHud hud)
    {
        _fixtures=fixtures;_camera=camera;_hud=hud;Layer=4;
        foreach(var fixture in fixtures)
        {
            var leader=new Line2D {Width=1,DefaultColor=fixture.TeamId==1?new(.3f,.75f,1,.8f):new(1,.4f,.4f,.8f),Visible=false};AddChild(leader);
            var panel=new PanelContainer {Name="Health_"+fixture.CharacterId,MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(panel);
            var box=new VBoxContainer {MouseFilter=Control.MouseFilterEnum.Ignore};panel.AddChild(box);box.AddThemeConstantOverride("separation",1);
            var text=new Label {HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};
            text.AddThemeFontSizeOverride("font_size",12);box.AddChild(text);
            var bar=new ProgressBar {MinValue=0,MaxValue=1,ShowPercentage=false,CustomMinimumSize=new(52,4),MouseFilter=Control.MouseFilterEnum.Ignore};
            var fill=new StyleBoxFlat {BgColor=fixture.TeamId==1?new(.1f,.7f,1):new(1,.2f,.28f)};
            bar.AddThemeStyleboxOverride("fill",fill);box.AddChild(bar);_health.Add(fixture.CharacterId,new(panel,bar,text,leader));
        }
        _historyPanel=new PanelContainer {Name="CombatHistory",Theme=hud.HudTheme,Visible=false,AnchorLeft=1,AnchorRight=1,OffsetLeft=-390,OffsetRight=-8,OffsetTop=62};AddChild(_historyPanel);
        var column=new VBoxContainer();_historyPanel.AddChild(column);
        _historyToggle=hud.HistoryButton;_historyToggle.ToggleMode=true;
        var close=MobaTacticalHud.Command(column,"Combat journal · close");close.Pressed+=ToggleHistory;
        _history=new RichTextLabel {Visible=false,CustomMinimumSize=new(350,260),ScrollActive=true,ScrollFollowing=true,BbcodeEnabled=false};
        _history.AddThemeFontSizeOverride("normal_font_size",14);column.AddChild(_history);
        _historyToggle.Toggled+=expanded=>{_history.Visible=expanded;_historyPanel.Visible=expanded;UpdateHistoryTitle();};
    }
    public void ToggleHistory()=>_historyToggle.ButtonPressed=!_historyToggle.ButtonPressed;
    private void UpdateHistoryTitle()=>_historyToggle.Text=$"Journal {Math.Max(0,_recordCount)} {(_history.Visible?"▾":"▸")}";
    public void Refresh(MobaHotseatSession session,string? commandTargetId=null)
    {
        if(_recordCount!=session.CombatHistory.Records.Count)
        {_recordCount=session.CombatHistory.Records.Count;_history.Text=session.CombatHistory.DisplayText();UpdateHistoryTitle();}
        // Clear stale bars/leaders first, including views removed since the previous refresh.
        foreach(var widget in _health.Values){widget.Root.Visible=false;widget.Leader.Visible=false;}
        var fixtures=_fixtures.Where(f=>GodotObject.IsInstanceValid(f)&&f.IsInsideTree()&&!f.IsQueuedForDeletion()
            &&session.Characters.TryGetValue(f.CharacterId,out var runtime)&&ReferenceEquals(runtime,f.RuntimeCharacter)).ToArray();
        var visible=MobaHealthBarVisibility.Resolve(session.SelectedFixtureId,commandTargetId,fixtures.Select(f=>f.Presentation!));
        var placed=new List<Rect2>();
        foreach(var fixture in fixtures.OrderBy(f=>f.CharacterId,StringComparer.Ordinal))
        {
            var state=fixture.Presentation!;var widget=_health[fixture.CharacterId];
            widget.Root.Visible=visible.Contains(fixture.CharacterId)&&state.ShowHealthBar&&fixture.VisibleToObserver&&!_camera.IsPositionBehind(fixture.GlobalPosition);
            widget.Text.Text=state.HealthText.Replace(" · ","\n");widget.Bar.Value=state.HealthFraction;
            widget.Root.ResetSize(); // Shrink again when the shield line disappears, including DEV reset.
            var anchor=_camera.UnprojectPosition(fixture.GlobalPosition+new Vector3(0,2.1f,0))+_hud.BattlefieldRect.Position;
            var desired=anchor-new Vector2(widget.Root.Size.X/2,widget.Root.Size.Y);var position=desired;
            widget.Root.Visible&=_hud.BattlefieldRect.Grow(-4).HasPoint(anchor);
            if(widget.Root.Visible)
            {
                while(placed.Any(r=>r.Grow(2).Intersects(new Rect2(position,widget.Root.Size))))position.Y-=widget.Root.Size.Y+5;
                position.X=Mathf.Clamp(position.X,_hud.BattlefieldRect.Position.X+4,_hud.BattlefieldRect.End.X-widget.Root.Size.X-4);
                widget.Root.Visible=position.Y>=_hud.BattlefieldRect.Position.Y+2;
                placed.Add(new(position,widget.Root.Size));
            }
            widget.Root.Position=position;widget.Leader.Visible=widget.Root.Visible&&position.DistanceTo(desired)>1;
            widget.Leader.Points=new[]{position+new Vector2(widget.Root.Size.X/2,widget.Root.Size.Y),anchor};
        }
    }
    public void Present(MobaCombatRecord record)
    {
        // Consume resolved strikes, including counters/follow-ups and misses. Damage reactions
        // and terminal death are synchronized from actual HP by the fixture's refresh.
        foreach(var entry in record.Events.Where(e=>e.Kind is MobaFeedbackKind.Opening or MobaFeedbackKind.Counter or MobaFeedbackKind.FollowUp))
        {
            var source=_fixtures.FirstOrDefault(f=>f.CharacterId==entry.SourceId);
            var target=_fixtures.FirstOrDefault(f=>f.CharacterId==entry.TargetId);
            if(source is not null&&target is not null)source.PresentAttack(target.GlobalPosition);
        }
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
            var view=_fixtures.FirstOrDefault(f=>GodotObject.IsInstanceValid(f)&&f.IsInsideTree()&&!f.IsQueuedForDeletion()&&f.CharacterId==popup.Event.TargetId);
            if(view is null){popup.Label.Visible=false;continue;}
            popup.Label.Visible=!_camera.IsPositionBehind(view.GlobalPosition);
            popup.Label.Position=_camera.UnprojectPosition(view.GlobalPosition+new Vector3(0,3.8f,0))
                +_hud.BattlefieldRect.Position-new Vector2(popup.Label.Size.X/2,(float)age*28);
            popup.Label.Visible&=_hud.BattlefieldRect.HasPoint(popup.Label.Position);
            popup.Label.Modulate=new Color(1,1,1,(float)Math.Min(1,(1.5-age)*2));
        }
    }
    public void Clear()
    {
        foreach(var popup in _popups){RemoveChild(popup.Label);popup.Label.QueueFree();}
        _popups.Clear();_nextPopup=_time;_recordCount=-1;_history.Text="";_historyToggle.ButtonPressed=false;
    }
}
