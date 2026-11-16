"""Refine versioned 2.3A source in Blender; isolated background export avoids live UI context issues."""
from pathlib import Path
import math
import json
import shutil
import bpy
import bmesh
import numpy as np

if bpy.app.background:
    bpy.ops.wm.read_factory_settings(use_empty=True)

ROOT = Path('D:/SoftwareEng/Game')
SOURCE = ROOT/'SourceAssets/levels/sources/ModularCliff'
VERSION = SOURCE/'Versions/Stage23B'
MAPS = ROOT/'SourceAssets/levels/textures/ModularCliff/Stage23B'
RUNTIME = ROOT/'Assets/Levels/Props/ModularCliff'
for directory in (VERSION,MAPS): directory.mkdir(parents=True,exist_ok=True)

def linear(v):
    return np.where(v <= .04045,v/12.92,((v+.055)/1.055)**2.4)

# One modest shared tiling set; all x-dependent functions are periodic at U0/U1.
size=512
u,v=np.meshgrid(np.linspace(0,1,size),np.linspace(0,1,size))
variation=(.45*np.sin(2*np.pi*u+1.4*np.sin(v*5))+
           .30*np.cos(4*np.pi*u-v*7)+.25*np.sin(2*np.pi*u+v*11))
distance=np.abs(np.sin(np.pi*(u-(.43+.10*np.sin(v*4)+.13*v))))/np.pi
fracture=np.exp(-(distance/.014)**2)*np.sin(np.pi*v)**2
weather=np.clip((v-.68)*1.6,0,.22)*(.6+.4*np.sin(2*np.pi*u+v*3))
base=np.array([58,65,70])/255
color=base[None,None,:]+variation[:,:,None]*np.array([7,8,9])[None,None,:]/255
color-=fracture[:,:,None]*np.array([15,15,14])[None,None,:]/255
color=color*(1-weather[:,:,None])+np.array([65,74,58])[None,None,:]/255*weather[:,:,None]
rough=np.clip(.87+.025*variation+.025*fracture,.82,.92)
height=.018*variation-.018*fracture
dy,dx=np.gradient(height,1/(size-1))
normal=np.stack((-dx*.20,-dy*.20,np.ones_like(u)),axis=-1)
normal/=np.linalg.norm(normal,axis=-1,keepdims=True)

def image(name,values,color_space):
    img=bpy.data.images.new('Cliff23B_'+name,width=size,height=size,alpha=True)
    img.colorspace_settings.name=color_space
    # Generated sRGB image pixels are stored as encoded values by Blender's PNG save.
    rgb=np.clip(values,0,1)
    rgba=np.concatenate((rgb,np.ones((size,size,1))),axis=-1).astype(np.float32)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw=str(MAPS/(name+'.png'));img.file_format='PNG';img.save();img.pack()
    return img

albedo=image('CliffAlbedo',color,'sRGB')
roughness=image('CliffRoughness',np.repeat(rough[:,:,None],3,axis=2),'Non-Color')
normal_map=image('CliffNormal',(normal+1)/2,'Non-Color')
material=bpy.data.materials.new('Cliff23B weathered dark slate')
material.use_nodes=True
nodes=material.node_tree.nodes;links=material.node_tree.links
bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
bsdf.inputs['Metallic'].default_value=0
bsdf.inputs['Roughness'].default_value=.88
for img,socket in ((albedo,'Base Color'),(roughness,'Roughness')):
    tex=nodes.new('ShaderNodeTexImage');tex.image=img;links.new(tex.outputs['Color'],bsdf.inputs[socket])
tex=nodes.new('ShaderNodeTexImage');tex.image=normal_map
nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.20
links.new(tex.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bsdf.inputs['Normal'])

with bpy.data.libraries.load(str(SOURCE/'Versions/Stage23A/ModularCliff.blend'),link=False) as (src,dst):
    dst.objects=[name for name in src.objects if name in ('ModularCliffFace','ModularCliffEdgeCap')]
work=bpy.context.scene if bpy.app.background else bpy.data.scenes.new('Stage23B_Cliff_Source')
work.name='Stage23B_Cliff_Source'
report={}
for obj in dst.objects:
    obj.name='ModularCliffEdgeCap' if 'EdgeCap' in obj.name else 'ModularCliffFace'
    work.collection.objects.link(obj)
    is_cap='EdgeCap' in obj.name
    original=obj.data.copy()
    # Subdivide existing triangles once, preserving every old mating segment.
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
    for vert in bm.verts:
        x,negative_front,h=vert.co
        front=-negative_front
        # Freeze all end planes, attachment, back and base. Only the exposed interior changes.
        if abs(x)>=.625-1e-6 or front<.40: continue
        window=max(0,math.sin(math.pi*(x+.625)/1.25))
        if is_cap:
            if -.18+1e-6 < h < -1e-6:
                vert.co.y+=.026*window*(.55+.45*math.sin(x*8+1))
            continue
        if h<=-1.85+1e-6 or h>=-.18-1e-6: continue
        t=(h+1.85)/1.67
        blend=window**.45*math.sin(math.pi*t)**.35
        bulge=(.062*math.exp(-((x+.25)/.30)**2-((h+.57)/.38)**2)+
               .052*math.exp(-((x-.27)/.27)**2-((h+1.16)/.40)**2)+
               .030*math.exp(-((x+.32)/.25)**2-((h+1.50)/.25)**2))
        crack_x=-.07+.21*(h+1)+.045*math.sin(h*4)
        crack=.105*math.exp(-((x-crack_x)/.047)**2)*math.sin(math.pi*t)**2
        split=.035*math.exp(-((h+1.22-.23*x)/.065)**2)
        desired=.465+.25*bulge-crack-split
        vert.co.y=-(front*(1-blend)+desired*blend)
        vert.co.z+=.045*blend*math.sin(x*7+h*3)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    assert all(len(e.link_faces)==2 for e in bm.edges)
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    if not is_cap:
        # Broad convex rock plates with irregular Voronoi outlines, recessed bevels and
        # planar faces. The original closed backing and every mating segment stay intact.
        seeds=[(-.34,-.46),(.25,-.42),(-.06,-.88),(.43,-1.02),
               (-.43,-1.19),(.05,-1.43),(.40,-1.69),(-.39,-1.70)]
        verts=[tuple(p.co) for p in obj.data.vertices]
        faces=[tuple(p.vertices) for p in obj.data.polygons]
        backing_faces=len(faces)
        for index,seed in enumerate(seeds):
            poly=[(-.56,-1.79),(.56,-1.79),(.56,-.23),(-.56,-.23)]
            for other in seeds:
                if other==seed:continue
                nx,nz=other[0]-seed[0],other[1]-seed[1]
                limit=(other[0]**2+other[1]**2-seed[0]**2-seed[1]**2)/2
                clipped=[]
                for a,b in zip(poly,poly[1:]+poly[:1]):
                    da=a[0]*nx+a[1]*nz-limit;db=b[0]*nx+b[1]*nz-limit
                    if da<=1e-9:clipped.append(a)
                    if (da<0)!=(db<0):
                        t=da/(da-db);clipped.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
                poly=clipped
            cx=sum(p[0] for p in poly)/len(poly);cz=sum(p[1] for p in poly)/len(poly)
            chipped=[]
            for j,(a,b) in enumerate(zip(poly,poly[1:]+poly[:1])):
                chipped.append(a)
                if (j+index)%3==0:
                    t=.38+.15*math.sin(index+j)
                    px=a[0]+t*(b[0]-a[0]);ph=a[1]+t*(b[1]-a[1])
                    chipped.append((px+(cx-px)*.10,ph+(cz-ph)*.10))
            poly=chipped
            rings=[]
            for ring in range(3):
                ids=[]
                for j,(x,h) in enumerate(poly):
                    shrink=(.97,.91+.045*math.sin(index*1.7+j*2.1),.97)[ring]
                    xx=cx+(x-cx)*shrink;hh=cz+(h-cz)*shrink
                    depth=(.49+.012*math.sin(index),.540+.035*math.sin(index*1.4)+.07*(xx-cx)*math.cos(index)+.04*(hh-cz)*math.sin(index),.425)[ring]
                    ids.append(len(verts));verts.append((xx,-depth,hh))
                rings.append(ids)
            outer,inner,back=rings
            faces.extend([tuple(inner),tuple(reversed(back))])
            for j in range(len(poly)):
                k=(j+1)%len(poly)
                faces.extend([(outer[j],outer[k],inner[k],inner[j]),(back[j],back[k],outer[k],outer[j])])
        rebuilt=bpy.data.meshes.new('Cliff23B irregular rock plates')
        rebuilt.from_pydata(verts,[],faces);rebuilt.update();obj.data=rebuilt
        bm=bmesh.new();bm.from_mesh(rebuilt)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        assert all(len(e.link_faces)==2 for e in bm.edges)
        bm.to_mesh(rebuilt);bm.free();rebuilt.update()
    # Smooth the rock interior while retaining sharp module backs/end caps.
    for polygon in obj.data.polygons:
        polygon.use_smooth=abs(polygon.normal.y)>.35 and (is_cap or polygon.index<backing_faces)
    # Shared world-oriented UVs keep front-face color/normal continuity across copies and cap.
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='Cliff continuous tiling UV')
    for polygon in obj.data.polygons:
        for li in polygon.loop_indices:
            co=obj.data.vertices[obj.data.loops[li].vertex_index].co
            x,z,h=co.x,-co.y,co.z
            uu=(x+.625)/1.25
            if abs(polygon.normal.z)>.8:
                vv=1+(z-.56)/1.85
            elif abs(polygon.normal.x)>.8:
                uu=(z+.625)/1.25;vv=(h+1.85)/1.85
            else: vv=(h+1.85)/1.85
            uv.data[li].uv=(uu,vv)
    obj.data.materials.clear();obj.data.materials.append(material)
    for polygon in obj.data.polygons:polygon.material_index=0
    # End-profile geometry equals the original, including the added collinear midpoints.
    for x in (-.625,.625):
        old=[v.co.copy() for v in original.vertices if abs(v.co.x-x)<1e-6]
        old_edges=[(original.vertices[e.vertices[0]].co,original.vertices[e.vertices[1]].co) for e in original.edges if all(abs(original.vertices[i].co.x-x)<1e-6 for i in e.vertices)]
        for point in [v.co for v in obj.data.vertices if abs(v.co.x-x)<1e-6]:
            def distance(edge):
                a,b=edge;line=b-a;t=max(0,min(1,(point-a).dot(line)/max(line.length_squared,1e-12)))
                return (point-(a+t*line)).length
            assert min(distance(e) for e in old_edges)<1e-6
    report[obj.name]={'vertices':len(obj.data.vertices),'triangles':len(obj.data.polygons),'dimensions_blender_xyz':list(obj.dimensions),'manifold':True,'original_mating_segments_preserved':True,'materials':1}

targets=list(work.objects)
with bpy.context.temp_override(scene=work,view_layer=work.view_layers[0]):
    for obj in targets:
        for other in targets:other.select_set(False)
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        filename='ModularCliffEdgeCap.glb' if 'EdgeCap' in obj.name else 'ModularCliffFace.glb'
        bpy.ops.export_scene.gltf(filepath=str(VERSION/filename),export_format='GLB',use_selection=True,export_animations=False,export_yup=True)
bpy.data.libraries.write(str(VERSION/'ModularCliff.blend'),{work},fake_user=True)
for filename in ('ModularCliffFace.glb','ModularCliffEdgeCap.glb'):
    shutil.copy2(VERSION/filename,RUNTIME/filename)
shutil.copy2(VERSION/'ModularCliff.blend',SOURCE/'ModularCliff.blend')
(ROOT/'artifacts/stage23b_blender_metrics.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
print('Stage23A originals preserved; Stage23B source and exports versioned; shared 512px albedo/normal/roughness maps packed.')
