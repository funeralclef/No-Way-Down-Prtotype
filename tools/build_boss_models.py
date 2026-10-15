"""Native guardian models: original geometry, no external asset dependencies."""
from pathlib import Path
import runpy
import math
ns = runpy.run_path(str(Path(__file__).with_name('build_enemy_models.py')))
Scene, colors = ns['Scene'], ns['COLORS']
colors.update(bellwarden=('0.83, 0.48, 0.16','1, 0.8, 0.38'), thornmaw=('0.63, 0.18, 0.31','0.98, 0.62, 0.72'), prismweaver=('0.24, 0.4, 0.74','0.58, 0.89, 1'))
def save(s):
    text='[gd_scene format=3]\n\n[ext_resource type="Script" path="res://EnemyCreatureMotion.cs" id="motion"]\n\n'
    (Path(__file__).resolve().parents[1]/'art/bosses'/f'{s.role}.tscn').write_text(text+'\n'.join(s.resources+s.nodes))
s=Scene('bellwarden');s.body((1.2,1.05,1.1),.78,.76);s.face((0,.96,-.76),.44,(1.1,.9,1),False,2)
s.orb('VelvetBelly',(0,.46,-.48),(1.2,.65,.3),.5,'tooth')
for i in range(3):s.ring('ResonantRib'+str(i),(0,.4+i*.36,.12),.86-i*.08,'accent')
s.ring('HollowCrown',(0,1.83,.12),.4,'accent');s.orb('CrownHeart',(0,1.78,.12),(.6,1,.6),.22,'glint')
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.76,1.17,.12),(0,0,side*-25))
 s.cone('CurledHorn',(0,.26,0),.2,.7,'armor',parent=p);s.orb('Bell',(side*.12,-.25,-.06),(1,.9,1),.3,'accent',p)
 s.orb('Clapper',(side*.12,-.53,-.06),(1,1.3,1),.1,'tooth',p)
save(s)
s=Scene('thornmaw');s.body((1.2,.84,1.25),.77,.66);s.face((0,.85,-.84),.44,(.67,1.05,1),True,3)
for i in range(7):
 a=(i/6-.5)*2.8;s.cone('RoseThorn'+str(i),(math.sin(a)*.85,.99,.35+math.cos(a)*.38),.16,.57,'accent',(0,0,-math.degrees(a)))
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.74,.46,.03))
 s.orb('PlushPaw',(side*.11,0,-.1),(1.15,.65,1.2),.36,'mob_body',p)
 s.cone('IvoryTusk',(side*.04,.3,-.5),.15,.65,'tooth',(22,0,side*-20),p)
s.node('TailGroup',(0,.55,.87));s.orb('Vine',(0,.12,.35),(.3,.3,1.5),.28,'armor','TailGroup');s.orb('RoseBud',(0,.16,.76),(1,.85,1),.25,'accent','TailGroup')
save(s)
s=Scene('prismweaver');s.body((.95,.96,1.1),.73,.83);s.face((0,.94,-.73),.46,(.67,1.15,1),False,1)
s.orb('SilkBelly',(0,.38,-.14),(.9,.7,.9),.49,'tooth')
for side in [-1,1]:
 p='LeftAppendage' if side<0 else 'RightAppendage';s.node(p,(side*.57,.92,.12),(0,side*-18,side*9))
 for i in range(3):s.fin('GlassWing'+str(i),(side*(.26+i*.23),0,.15+i*.18),(.6,.14,1.1-i*.16),'accent',(0,side*(i*16),0),p)
 s.orb('DreamPearl',(side*.84,.02,.53),(1,1,1),.12,'glint',p)
for i in range(3):s.cone('CrystalCrown'+str(i),((i-1)*.32,1.68,.08),.16,.65-abs(i-1)*.18,'accent',(0,0,(1-i)*20))
s.node('TailGroup',(0,.64,.72));s.fin('SilkTail',(0,0,.39),(.8,.1,.95),'armor',parent='TailGroup')
save(s)
colors['seal']=('0.4, 0.55, 0.7','0.65, 1, 0.88')
s=Scene('seal');s.orb('DreamCore',(0,.7,0),(.8,1,.8),.4,'glint')
for i in range(4):
 a=i*math.pi/2;s.fin('CrystalPetal'+str(i),(math.cos(a)*.35,.7,math.sin(a)*.35),(.3,1.15,.34),'mob_body',(0,-math.degrees(a),0))
s.ring('SealOrbit',(0,.67,0),.58,'accent',(25,0,0));s.cone('SealTip',(0,1.2,0),.18,.4,'accent')
save(s)
