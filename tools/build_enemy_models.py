"""Author the nine native creature scenes. No gameplay or collision data lives here."""
from pathlib import Path
import math
ROOT = Path(__file__).resolve().parents[1]
COLORS = {
 'springboard':('.48, 1, .25','.9, 1, .28'), 'spear':('1, .18, .08','1, .68, .08'),
 'courier':('.08, .94, 1','.65, 1, 1'), 'shepherd':('.2, .52, 1','.42, .9, 1'),
 'trickster':('.84, .2, 1','1, .52, 1'), 'shell':('1, .58, .12','1, .9, .42'),
 'relay':('.72, 1, .16','1, 1, .72'), 'diver':('1, .08, .38','1, .78, .22'),
 'twin':('.24, .48, 1','.45, 1, .92')}
def vec(v): return 'Vector3('+', '.join(f'{n:.5g}' for n in v)+')'
class Scene:
 def __init__(self, role):
  self.role=role; self.resources=[]; self.nodes=[]; self.serial=0
  body,accent=COLORS[role]
  for name,color in [('mob_body',body),('accent',accent),('armor','.12, .16, .18'),('mob_eye','.94, .96, .87'),('pupil','.025, .05, .065'),('tooth','.98, .92, .74'),('glint','1, 1, 1')]:
   color=', '.join(str(float(channel.strip())) for channel in color.split(','))
   extra='\nemission_enabled = true\nemission = Color('+color+', 1)\nemission_energy_multiplier = 0.4' if name in ('accent','glint') else ''
   self.resources.append(f'[sub_resource type="StandardMaterial3D" id="{name}"]\nresource_name = "{name}"\nalbedo_color = Color({color}, 1)\nroughness = 0.48\nmetallic = 0.04{extra}\n')
  self.nodes.append('[node name="Creature" type="Node3D"]\nscript = ExtResource("motion")\n')
 def node(self,name,pos=(0,0,0),rot=(0,0,0),parent='.'):
  self.nodes.append(f'[node name="{name}" type="Node3D" parent="{parent}"]\nposition = {vec(pos)}\nrotation_degrees = {vec(rot)}\n')
 def mesh(self,name,kind,props,mat,pos,scale=(1,1,1),rot=(0,0,0),parent='.'):
  self.serial+=1; ident=f'm{self.serial}'
  self.resources.append(f'[sub_resource type="{kind}" id="{ident}"]\nmaterial = SubResource("{mat}")\n{props}\n')
  self.nodes.append(f'[node name="{name}" type="MeshInstance3D" parent="{parent}"]\nposition = {vec(pos)}\nscale = {vec(scale)}\nrotation_degrees = {vec(rot)}\nmesh = SubResource("{ident}")\n')
 def orb(self,name,pos,scale=(1,1,1),r=.5,mat='mob_body',parent='.',rot=(0,0,0)):
  self.mesh(name,'SphereMesh',f'radius = {r}\nheight = {r*2}\nradial_segments = 24\nrings = 12',mat,pos,scale,rot,parent)
 def cone(self,name,pos,r=.15,h=.4,mat='tooth',rot=(0,0,0),parent='.'):
  self.mesh(name,'CylinderMesh',f'top_radius = 0.015\nbottom_radius = {r}\nheight = {h}\nradial_segments = 12',mat,pos,rot=rot,parent=parent)
 def ring(self,name,pos,r=.3,mat='accent',rot=(0,0,0),parent='.'):
  self.mesh(name,'TorusMesh',f'inner_radius = {r}\nouter_radius = {r+.065}\nrings = 24\nring_segments = 8',mat,pos,rot=rot,parent=parent)
 def fin(self,name,pos,size,mat='accent',rot=(0,0,0),parent='.'):
  self.mesh(name,'PrismMesh',f'size = {vec(size)}',mat,pos,rot=rot,parent=parent)
 def body(self,scale=(1,1,1),r=.7,y=.65): self.orb('Body',(0,y,0),scale,r)
 def face(self,point=(0,.86,-.64),r=.4,pupil=(1,1,1),brow=False,fangs=2,name='Eyes',mouth=True):
  self.node(name,point,(30,0,0)); self.orb('Socket',(0,0,0),(1.13,1.1,.30),r,'armor',name)
  self.node('Blink',parent=name)
  eye=name+'/Blink'
  self.orb('Sclera',(0,0,-.075),(1,1,.30),r,'mob_eye',eye)
  self.orb('Pupil',(0,.005,-.19),(pupil[0],pupil[1],.22),r*.53,'pupil',eye)
  self.orb('Glint',(-r*.19,r*.23,-.245),(1,1,.28),r*.13,'glint',eye)
  if brow:
   for side in [-1,1]: self.orb('Brow'+str(side),(side*r*.43,r*.65,-.13),(1.1,.23,.26),r*.6,'armor',name,rot=(0,0,side*16))
  if mouth:
   self.orb('Mouth',(0,-r-.065,-.035),(1,.30,.25),r*.60,'pupil',name)
   for i in range(fangs):
    x=0 if fangs==1 else (i-(fangs-1)/2)*r*.42
    self.cone('Fang'+str(i),(x,-r-.07,-.105),r*.10,r*.26,'tooth',(180,0,0),name)
 def ears(self,length=.45,spread=.55,mat='mob_body',droop=0):
  for side in [-1,1]:
   parent='LeftAppendage' if side<0 else 'RightAppendage';self.node(parent,(side*spread,1.15,.12),(0,0,side*(-25+droop)))
   self.orb('SoftEar',(0,length*.25,0),(.44,1,.65),length*.65,mat,parent)
 def save(self):
  text='[gd_scene format=3]\n\n[ext_resource type="Script" path="res://EnemyCreatureMotion.cs" id="motion"]\n\n'
  (ROOT/'art/enemies'/f'{self.role}.tscn').write_text(text+'\n'.join(self.resources+self.nodes))

s=Scene('springboard');s.body((1,.88,1),.73,.65);s.orb('Belly',(0,.39,-.13),(1,.72,1),.57)
s.face(r=.43,pupil=(1.08,1.08,1));s.ears(.42,.51)
s.ring('BounceCrown',(0,1.24,.19),.33)
for side in [-1,1]: s.orb('Cheek'+str(side),(side*.47,.64,-.48),(1,.48,.6),.13,'accent')
s.save()

s=Scene('spear');s.body((.94,1.05,1.15),.64,.7);s.face((0,.88,-.68),.38,(.65,1.1,1),True,3)
for side in [-1,1]:
 s.orb('Haunch'+str(side),(side*.4,.31,.22),(.9,.7,1.1),.27)
 s.node('LeftAppendage' if side<0 else 'RightAppendage',(side*.44,1.16,.1),(0,0,side*-28))
 p='LeftAppendage' if side<0 else 'RightAppendage';s.cone('HornBase',(0,.17,0),.17,.45,'armor',parent=p);s.cone('HornTip',(0,.43,-.04),.1,.28,'accent',rot=(-25,0,side*20),parent=p)
s.cone('NasalLance',(0,1.15,-.57),.12,.5,'accent',(-60,0,0));s.node('TailGroup',(0,.55,.62))
s.fin('ArrowTail',(0,0,.32),(.57,.12,.7),'accent',parent='TailGroup');s.save()

s=Scene('courier');s.body((1,.8,1.02),.65,.66);s.face((0,.78,-.61),.43,(1.12,1.15,1),fangs=1)
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.59,.72,.05),(0,side*24,side*9))
 s.orb('MothWing',(side*.28,0,.12),(1.25,.24,1.65),.46,'accent',p)
 s.orb('WingRoot',(0,-.05,.1),(.5,.5,1.1),.25,'mob_body',p)
 s.cone('WingClaw',(side*.58,0,.45),.07,.26,'armor',(85,0,0),p)
 s.orb('Antenna'+str(side),(side*.27,1.24,.05),(.5,1.2,.65),.16,'accent')
s.save()

s=Scene('shepherd');s.body((1.04,.82,1),.77,.65);s.orb('SoftMantle',(0,.48,.15),(1.15,.63,1),.72,'armor')
s.face((0,.85,-.7),.42,(1.08,.7,1),fangs=2)
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.72,.87,.08))
 s.orb('Arm',(side*.12,-.23,0),(.65,1.1,.8),.28,'mob_body',p)
 s.cone('LanternHorn',(0,.39,.1),.14,.65,'mob_body',(0,0,side*-18),p)
 s.orb('Lantern',(side*.13,.76,.1),(1,1.1,1),.15,'accent',p)
s.orb('LowerLip',(0,.39,-.58),(1.5,.32,.5),.18,'accent');s.save()

s=Scene('trickster');s.body((.93,1.02,1),.65,.72);s.face((-.04,.95,-.62),.44,(.7,1.1,1),True,3)
s.node('LeftAppendage',(-.44,1.2,.1),(0,0,25));s.cone('LongHorn',(0,.26,0),.18,.67,'armor',parent='LeftAppendage');s.cone('CurledTip',(-.04,.61,0),.105,.35,'accent',(0,0,55),'LeftAppendage')
s.node('RightAppendage',(.46,1.16,.1),(0,0,-55));s.cone('ShortHorn',(0,.21,0),.19,.49,'accent',parent='RightAppendage')
s.node('TailGroup',(.1,.5,.6));s.orb('TailCurl',(0,.06,.23),(.35,.7,1.3),.24,'mob_body','TailGroup');s.fin('TailBarb',(.1,.12,.53),(.45,.1,.46),'accent',parent='TailGroup')
for side in [-1,1]: s.orb('Freckle'+str(side),(side*.48,.7,-.47),(1,.6,.5),.09,'accent')
s.save()

s=Scene('shell');s.body((1,.85,1.12),.8,.64);s.face((0,.81,-.79),.38,(.8,.9,1),True,2)
s.orb('Carapace',(0,.83,.34),(1,.68,.9),.76,'armor')
for i in range(3):
 s.orb('ShellScute'+str(i),((i-1)*.41,1.17,.36),(.8,.35,1.2),.35,'accent')
s.ring('ShellCurl',(0,1.29,.38),.2,'armor')
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.62,.24,-.13));s.orb('Paw',(0,0,0),(1.1,.65,1.3),.24,'mob_body',p)
 s.cone('Claw',(0,0,-.28),.065,.18,'tooth',(-90,0,0),p)
 s.cone('BackSpike'+str(side),(side*.54,1.14,.48),.15,.35,'armor',(0,0,side*-32))
s.save()

s=Scene('relay');s.body((.95,.9,.95),.65,.85);s.face((0,.92,-.6),.43,(.75,.85,1),fangs=1)
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.39,.42,.02));s.orb('Wisp',(side*.06,-.13,.05),(.46,1,.6),.27,'mob_body',p)
s.orb('GhostTip',(0,.25,.14),(.42,.9,.55),.26)
s.orb('AntennaStem',(0,1.45,.21),(.3,1.3,.3),.18,'mob_body');s.ring('UplinkCrown',(0,1.72,.22),.24,'accent',(55,0,0))
s.orb('SignalPearl',(0,1.72,.22),(1,1,1),.13,'accent');s.save()

s=Scene('diver');s.body((.91,.9,1.2),.62,.72);s.face((0,.88,-.72),.37,(.62,1.08,1),True,2)
s.fin('DorsalFin',(0,1.18,.2),(.16,.7,.85),'armor')
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.5,.75,.16),(0,side*-30,0))
 s.orb('RayFin',(side*.27,0,.05),(1.3,.2,1.1),.42,'mob_body',p)
 s.fin('WingPoint',(side*.53,-.015,.22),(.46,.08,.64),'armor',(0,side*30,0),p)
s.node('TailGroup',(0,.66,.7));s.orb('TailStem',(0,0,.25),(.25,.25,1.2),.25,'mob_body','TailGroup');s.fin('CrescentTail',(0,0,.53),(.95,.1,.48),'accent',parent='TailGroup');s.save()

s=Scene('twin');s.orb('SharedBelly',(0,.43,.08),(1.3,.78,1),.5)
for side in [-1,1]:
 s.orb('Lobe'+str(side),(side*.34,.79,0),(1,1.1,1),.51)
 s.face((side*.34,.91,-.49),.3,(1.08,.95,1) if side<0 else (.65,1.1,1),side>0,1,'Eyes' if side<0 else 'SecondEyes')
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.69,1.13,.05),(0,0,side*-45));s.orb('FloppyEar',(0,.12,0),(.6,1,.5),.24,'accent',p)
s.orb('Bond',(0,.52,-.46),(.42,1,.32),.15,'accent');s.save()
