using System;
using System.Collections.Generic;
using UnityEngine;

/**
 * Manages hardcoded bow anatomy data.
 */
namespace ValheimVRMod.Utilities
{
  public class BowAnatomy
  {
    public readonly Vector3 stringTop;
    public readonly Vector3 stringBottom;
    public readonly Vector3 handleTop;
    public readonly Vector3 handleBottom;
    public readonly float handleWidth;
    public readonly float handleHeight;
    public readonly float softLimbHeight;
    public readonly float stringRadius;
    
    private static BowAnatomy DefaultBowAnatomy = new BowAnatomy(new Vector3(0, 0.75f, 0.325f), new Vector3(0, -0.75f, 0.325f), new Vector3(0, 0.312f, 0), new Vector3(0, -0.312f, 0), 0.05f, 0.624f, 0.125f, 0.01f);
    private static Dictionary<string, BowAnatomy> BowAnatomies = new Dictionary<string, BowAnatomy>
    {
        {"$item_bow_spinesnap", new BowAnatomy(new Vector3(0, 0.75f, 0.325f), new Vector3(0, -0.75f, 0.325f), new Vector3(0, 0.312f, 0), new Vector3(0, -0.312f, 0), 0.05f, 0.624f, 0.125f, 0.01f)}
    };
           
    protected BowAnatomy(Vector3 stringTop, Vector3 stringBottom, Vector3 handleTop, Vector3 handleBottom, float handleWidth, float handleHeight, float softLimbHeight, float stringRadius) {
      this.stringTop = stringTop;
      this.stringBottom = stringBottom;
      this.handleTop = handleTop;
      this.handleBottom = handleBottom;
      this.handleWidth = handleWidth;
      this.handleHeight = handleHeight;
      this.softLimbHeight = softLimbHeight;
      this.stringRadius = stringRadius;
    }
    
    public static BowAnatomy getBowAnatomy(string bowName) {
      if (BowAnatomies.ContainsKey(bowName)) {
        return BowAnatomies[bowName];
      } else {
        return DefaultBowAnatomy;
      }
    }
  }
}
