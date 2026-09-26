using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoHomeButton : HomeButton
{
    private bool goHome = true;
    public override bool GoHome => goHome;
}
