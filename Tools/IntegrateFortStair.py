"""Append the independent decorative connection scene without rebuilding the battlefield."""
from pathlib import Path
R=Path(__file__).resolve().parents[1];p=R/'Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn';t=p.read_text()
if 'id="FortStairIntegration"' not in t:
 i=t.index('[sub_resource');t=t[:i]+'[ext_resource type="PackedScene" path="res://Assets/Levels/Environment/FortStairIntegration/FortStairIntegration.tscn" id="FortStairIntegration"]\n\n'+t[i:]
 t+='\n[node name="FortStairIntegration" parent="." instance=ExtResource("FortStairIntegration")]\n';p.write_text(t)
print('Integrated one independent decorative masonry root; original nodes unchanged.')
