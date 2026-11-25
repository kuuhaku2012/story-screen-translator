import unittest
from bubble_text_regions import fuse_regions
def line(x,y,w=30,h=10):return {'text':'','box':[[x,y],[x+w,y],[x+w,y+h],[x,y+h]]}
class FusionTests(unittest.TestCase):
    def test_background_does_not_expand_card_far_beyond_text(self):
        r=fuse_regions([{'box':[0,0,200,250]}],[line(130,30,40,10),line(130,42,40,10)])
        b=r['regions'][0]['regionBounds']
        self.assertLess(b[2]-b[0],100)
        self.assertLess(b[3]-b[1],100)

    def test_continuous_caption_crossing_bubble_boundary_is_not_split(self):
        r=fuse_regions([{'box':[0,24,100,100]}],[line(20,10,60,20),line(20,27,60,20),line(20,44,60,20)])
        self.assertEqual(len(r['regions']),1)

    def test_art_without_text_removed(self):
        r=fuse_regions([{'box':[0,0,80,80]},{'box':[100,0,180,80]}],[line(15,20),line(15,32)])
        self.assertEqual(r['removedBubbleCount'],1);self.assertEqual(len(r['regions']),1)

    def test_overlapping_tall_caption_fragment_is_absorbed(self):
        r=fuse_regions([],[line(0,0,70,15),line(0,14,70,15),line(0,28,70,15),line(15,25,60,35)])
        self.assertEqual(len(r['regions']),1)

    def test_weak_overlap_is_not_enough_to_merge_distinct_clusters(self):
        r=fuse_regions([],[line(0,0,70,15),line(0,14,70,15),line(0,28,70,15),line(15,34,60,35)])
        self.assertEqual(len(r['regions']),2)
    def test_connected_balloon_splits_and_floating_text_survives(self):
        r=fuse_regions([{'box':[0,0,200,90]}],[line(10,20),line(10,32),line(140,20),line(140,32),line(250,20)])
        self.assertEqual(len(r['regions']),3)
        self.assertEqual(sum(g['kind']=='cluster-in-connected-bubble' for g in r['regions']),2)
        self.assertEqual(sum(g['kind']=='floating-text' for g in r['regions']),1)
if __name__=='__main__':unittest.main()
