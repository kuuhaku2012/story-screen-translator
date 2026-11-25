import unittest
from manga_line_regions import group_lines

def line(text,x,y,w,h):
    return {'text':text,'box':[[x,y],[x+w,y],[x+w,y+h],[x,y+h]]}

class GroupingTests(unittest.TestCase):
    def test_wide_heading_does_not_absorb_centered_dialogue(self):
        blocks=[line('CHAPTER HEADING',0,0,600,40),line('WHAT',260,65,55,28),line('TOOK YOU',240,90,95,28)]
        self.assertEqual(sorted(r['detectedLineCount'] for r in group_lines(blocks)),[1,2])

    def test_overlapping_boxes_of_centered_dialogue_join(self):
        blocks=[line('YOU',20,0,60,40),line('HEAR ME',0,28,100,40),line('KABURA?',5,57,90,36)]
        self.assertEqual(len(group_lines(blocks)),1)

    def test_separate_columns_remain_separate(self):
        blocks=[line('A',0,0,50,20),line('B',0,20,50,20),line('C',180,0,50,20),line('D',180,20,50,20)]
        self.assertEqual(len(group_lines(blocks)),2)

    def test_short_pronoun_joins_normal_width_line(self):
        blocks=[line('I',30,0,9,20),line('KNOW',10,18,60,20),line('!!',25,36,20,20)]
        self.assertEqual(len(group_lines(blocks)),1)

if __name__=='__main__': unittest.main()
