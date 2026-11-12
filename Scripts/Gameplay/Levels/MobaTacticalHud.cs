using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Gameplay.Levels;

/// <summary>Containers, portrait and responsive reservations only. The battlefield binds existing commands.</summary>
public partial class MobaTacticalHud : CanvasLayer
{
    public Control Root {get;private set;}=null!;
    public PanelContainer Bottom {get;private set;}=null!;
    public SubViewportContainer Battlefield {get;private set;}=null!;
    public SubViewport WorldView {get;private set;}=null!;
    public VBoxContainer TopLeft {get;private set;}=null!;
    public HBoxContainer TopRight {get;private set;}=null!;
    public VBoxContainer Developer {get;private set;}=null!;
    public Control ContextHost {get;private set;}=null!;
    public VBoxContainer Details {get;private set;}=null!;
    public GridContainer Commands {get;private set;}=null!;
    public Label Message {get;private set;}=null!;
    public Label Context {get;private set;}=null!;
    public Label NameText {get;private set;}=null!;
    public Label StatsText {get;private set;}=null!;
    public Label EffectsText {get;private set;}=null!;
    public Label InventoryText {get;private set;}=null!;
    public Label HealthText {get;private set;}=null!;
    public Button HelpButton {get;private set;}=null!;
    public Button DeveloperButton {get;private set;}=null!;
    public Button HistoryButton {get;private set;}=null!;
    public Label Timer {get;private set;}=null!;
    public Rect2 BattlefieldRect=>Battlefield.GetGlobalRect();
    public PanelContainer DeveloperPanel {get;private set;}=null!;
    private PanelContainer _help=null!;
    private Label _identity=null!,_emptyPortrait=null!;
    private ProgressBar _health=null!;
    private SubViewport _portrait=null!;
    private Node3D _portraitRoot=null!;
    private Camera3D _portraitCamera=null!;
    private Node3D? _portraitModel;
    private string? _portraitId;
    public string? PortraitId=>_portraitId;
    public bool HelpVisible=>_help.Visible;
    public Theme HudTheme {get;private set;}=null!;
    private Vector2 _lastSize;
    public event Action? LayoutChanged;

    public void Initialize(World3D world,Camera3D camera)
    {
        Layer=3;HudTheme=GD.Load<Theme>("res://Resources/UI/MobaTacticalTheme.tres");
        Root=new Control {Name="TacticalHud",MouseFilter=Control.MouseFilterEnum.Ignore,Theme=HudTheme};AddChild(Root);Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Battlefield=new SubViewportContainer {Name="BattlefieldViewport",Stretch=true,MouseFilter=Control.MouseFilterEnum.Ignore};Root.AddChild(Battlefield);
        WorldView=new SubViewport {Name="TacticalWorld",World3D=world,HandleInputLocally=false,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};Battlefield.AddChild(WorldView);
        camera.Reparent(WorldView,true);camera.Current=true;
        var top=Panel("MatchBar",Root);top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);top.OffsetBottom=54;
        var topRow=new HBoxContainer();top.AddChild(topRow);
        TopLeft=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};topRow.AddChild(TopLeft);TopLeft.AddThemeConstantOverride("separation",0);
        Timer=new Label {HorizontalAlignment=HorizontalAlignment.Center,CustomMinimumSize=new(142,0)};Timer.AddThemeFontSizeOverride("font_size",23);topRow.AddChild(Timer);
        TopRight=new HBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,Alignment=BoxContainer.AlignmentMode.End};topRow.AddChild(TopRight);
        Bottom=Panel("UnitDeck",Root);Bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        var deck=new VBoxContainer();Bottom.AddChild(deck);
        var regions=new HBoxContainer {SizeFlagsVertical=Control.SizeFlags.ExpandFill};regions.AddThemeConstantOverride("separation",8);deck.AddChild(regions);
        var portraitColumn=Region("SELECTED UNIT",regions,276,out var portraitBody);
        var portraitRow=new HBoxContainer();portraitBody.AddChild(portraitRow);
        var portraitFrame=Panel("PortraitFrame",portraitRow);portraitFrame.CustomMinimumSize=new(82,108);
        var portraitTexture=new SubViewportContainer {Stretch=true,CustomMinimumSize=new(80,104),MouseFilter=Control.MouseFilterEnum.Ignore};portraitFrame.AddChild(portraitTexture);
        _portrait=new SubViewport {Size=new(160,208),OwnWorld3D=true,TransparentBg=false,RenderTargetUpdateMode=SubViewport.UpdateMode.Disabled};portraitTexture.AddChild(_portrait);
        _portraitRoot=new Node3D();_portrait.AddChild(_portraitRoot);
        _portrait.AddChild(new WorldEnvironment {Environment=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new(.035f,.05f,.065f),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.5f,TonemapMode=Godot.Environment.ToneMapper.Filmic}});
        _portraitRoot.AddChild(new DirectionalLight3D {RotationDegrees=new(-25,-35,0),LightEnergy=1.1f});
        _portraitRoot.AddChild(new DirectionalLight3D {RotationDegrees=new(-15,145,0),LightEnergy=.4f});
        _portraitCamera=new Camera3D {Projection=Camera3D.ProjectionType.Orthogonal,Current=true};_portraitRoot.AddChild(_portraitCamera);
        _emptyPortrait=new Label {Text="◇",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};_emptyPortrait.AddThemeFontSizeOverride("font_size",34);portraitFrame.AddChild(_emptyPortrait);
        var identityColumn=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};portraitRow.AddChild(identityColumn);
        NameText=Text(identityColumn,"No unit selected",16);NameText.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        _identity=Text(identityColumn,"Select a friendly unit",12);HealthText=Text(identityColumn,"—",12);
        _health=new ProgressBar {MinValue=0,MaxValue=1,ShowPercentage=false,CustomMinimumSize=new(0,7)};_health.AddThemeStyleboxOverride("fill",new StyleBoxFlat {BgColor=new(.2f,.67f,.46f)});portraitBody.AddChild(_health);
        var detailPanel=Region("UNIT DETAILS",regions,0,out var detailBody);detailPanel.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;detailPanel.SizeFlagsStretchRatio=1;
        ContextHost=new Control {SizeFlagsVertical=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(235,130)};detailBody.AddChild(ContextHost);
        Details=new VBoxContainer();ContextHost.AddChild(Details);Details.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        StatsText=Text(Details,"",13);EffectsText=Text(Details,"",12);EffectsText.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        Context=Text(Details,"Select a unit to inspect available actions.",12);Context.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        Region("COMMANDS",regions,300,out var actionBody);Commands=new GridContainer {Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};actionBody.AddChild(Commands);
        Region("EQUIPMENT / ITEMS",regions,180,out var inventoryBody);InventoryText=Text(inventoryBody,"No unit selected",13);InventoryText.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        Message=Text(deck,"Select a friendly unit. F1 for controls.",12);Message.MaxLinesVisible=2;Message.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
        DeveloperPanel=Panel("Development",Root);DeveloperPanel.Visible=false;DeveloperPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);DeveloperPanel.OffsetLeft=-440;DeveloperPanel.OffsetRight=-8;DeveloperPanel.OffsetTop=62;DeveloperPanel.OffsetBottom=350;
        var devScroll=new ScrollContainer {CustomMinimumSize=new(400,260),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};DeveloperPanel.AddChild(devScroll);Developer=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};devScroll.AddChild(Developer);Text(Developer,"DEVELOPMENT CONTROLS",15);
        _help=Panel("ControlsHelp",Root);_help.Visible=false;_help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);_help.Position=new(8,62);_help.CustomMinimumSize=new(460,0);
        Text(_help,"TACTICAL CONTROLS · F1 to close\n\nLeft click: select / choose destination or target\nRight click: cancel targeting and deselect\nWASD: pan · Q/E or middle drag: rotate\nMouse wheel: zoom · Home: fit battlefield · G: grid\n\nMove: show legal routes · Attack: preview then confirm\nAbilities: preview, choose target, confirm or cancel\nTransfer: choose a legal destination, then confirm\nEnd: skip remaining actions and hand over control\n\nJournal: combat history · DEV: timer, pause, reset\nThinking and targeting use the activation clock.",14);
        HistoryButton=Command(TopRight,"Journal");DeveloperButton=Command(TopRight,"DEV");HelpButton=Command(TopRight,"? F1");
        HelpButton.Pressed+=ToggleHelp;DeveloperButton.Pressed+=()=>DeveloperPanel.Visible=!DeveloperPanel.Visible;
        Root.Resized+=ResizeHud;ResizeHud();
    }
    public static Label Text(Node parent,string text,int size=14)
    {var label=new Label {Text=text,MouseFilter=Control.MouseFilterEnum.Ignore};label.AddThemeFontSizeOverride("font_size",size);parent.AddChild(label);return label;}
    private static PanelContainer Panel(string name,Node parent)
    {var panel=new PanelContainer {Name=name};parent.AddChild(panel);return panel;}
    private static PanelContainer Region(string title,Node parent,float minimum,out VBoxContainer body)
    {var panel=Panel(title.Replace(" / ","_"),parent);panel.CustomMinimumSize=new(minimum,0);body=new VBoxContainer();panel.AddChild(body);var heading=Text(body,title,12);heading.Modulate=new(.79f,.68f,.45f);return panel;}
    public static Button Command(Node parent,string text)
    {var b=new Button {Text=text,FocusMode=Control.FocusModeEnum.None,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};parent.AddChild(b);return b;}
    public void ToggleHelp()=>_help.Visible=!_help.Visible;
    private void ResizeHud()
    {
        var size=Root.Size;if(size.X<1||size.Y<1)return;
        var metrics=MobaHudMetrics.For(Math.Max(1280,size.X),Math.Max(720,size.Y));
        Bottom.OffsetTop=-(float)metrics.Bottom;Bottom.OffsetBottom=0;
        Battlefield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);Battlefield.OffsetTop=(float)metrics.Top;Battlefield.OffsetBottom=-(float)metrics.Bottom;
        if(size!=_lastSize){_lastSize=size;LayoutChanged?.Invoke();}
    }
    public void Refresh(MobaHotseatSession session,StaticTacticalCharacter? selected)
    {
        var p=MobaHudPresentation.Capture(session);NameText.Text=p.Name;_identity.Text=p.Team+"\n"+p.Readiness;HealthText.Text=p.Health;_health.Value=p.HealthFraction;StatsText.Text=p.Stats;EffectsText.Text=p.Effects;EffectsText.TooltipText=p.Effects;InventoryText.Text=p.Inventory;
        _identity.Modulate=session.SelectedCharacter?.TeamId==1?new(.4f,.78f,1):session.SelectedCharacter?.TeamId==2?new(1,.45f,.48f):Colors.LightGray;
        RefreshPortrait(selected);
    }
    private void RefreshPortrait(StaticTacticalCharacter? selected)
    {
        if(_portraitId==selected?.CharacterId)return;
        _portraitId=selected?.CharacterId;
        if(_portraitModel is not null){_portraitRoot.RemoveChild(_portraitModel);_portraitModel.QueueFree();_portraitModel=null;}
        _emptyPortrait.Visible=selected is null;_portrait.RenderTargetUpdateMode=selected is null?SubViewport.UpdateMode.Disabled:SubViewport.UpdateMode.Once;
        if(selected is null)return;
        // One presentation-only instance; mesh/material resources remain shared with the original import.
        var source=selected.GetNode<Node3D>(selected.ModelNodePath);
        _portraitModel=GD.Load<PackedScene>(selected.AuthoredModelPath!).Instantiate<Node3D>();_portraitRoot.AddChild(_portraitModel);_portraitModel.Transform=source.Transform;
        var points=new List<Vector3>();
        void Inspect(Node n)
        {
            n.ProcessMode=ProcessModeEnum.Disabled;if(n is AnimationPlayer a){a.Stop();a.Active=false;}
            if(n is CollisionObject3D c){c.CollisionLayer=0;c.CollisionMask=0;}
            if(n is MeshInstance3D m&&m.Mesh is not null){var box=m.GetAabb();for(int i=0;i<8;i++)points.Add(m.GlobalTransform*box.GetEndpoint(i));}
            foreach(Node child in n.GetChildren())Inspect(child);
        }
        Inspect(_portraitModel);if(points.Count==0)return;
        float min=points.Min(p=>p.Y),max=points.Max(p=>p.Y),height=max-min;
        var focus=new Vector3((points.Min(p=>p.X)+points.Max(p=>p.X))/2,min+height*.83f,(points.Min(p=>p.Z)+points.Max(p=>p.Z))/2);
        _portraitCamera.Size=height*.5f;_portraitCamera.Position=focus+new Vector3(0,.015f,4);_portraitCamera.LookAt(focus);
    }
}
