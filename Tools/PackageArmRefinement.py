"""Create matched pose comparison sheets from the Godot verification renders."""
from PIL import Image, ImageDraw, ImageFont
from AuditCharacterTexturing import ROOT,MODELS

OUT=ROOT/'artifacts/arm_pose_refinement'
LABELS=['Hooded Assassin','Gentle Guardian','Time Weaver','Nigel','Gravebound Colossus','Beothuk Spirit Guardian']

if __name__=='__main__':
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',25)
    sheet=Image.new('RGB',(1600,2940),(23,28,36));draw=ImageDraw.Draw(sheet)
    roster=Image.new('RGB',(1500,980),(23,28,36));rd=ImageDraw.Draw(roster)
    for i,(name,label) in enumerate(zip(MODELS,LABELS)):
        y=i*490;draw.text((20,y+8),label,font=font,fill='white')
        draw.text((560,y+8),'Before',font=font,fill='#aab6c7')
        draw.text((1340,y+8),'Corrected',font=font,fill='#8edbb3')
        for j,version in enumerate(('before','after')):
            image=Image.open(OUT/f'comparison_renders/{name}_{version}.png').convert('RGB')
            sheet.paste(image.resize((800,450),Image.Resampling.LANCZOS),(j*800,y+40))
        image=Image.open(OUT/f'comparison_renders/{name}_after.png').convert('RGB')
        image=image.crop((360,30,920,710)).resize((370,450),Image.Resampling.LANCZOS)
        x=(i%3)*500;y=(i//3)*490
        roster.paste(image,(x+65,y+40));rd.text((x+20,y+8),label,font=font,fill='white')
    sheet.save(OUT/'before_after.png');roster.save(OUT/'corrected_roster.png')
    print('Saved corrected roster and matched arm-pose comparison for all six characters')
