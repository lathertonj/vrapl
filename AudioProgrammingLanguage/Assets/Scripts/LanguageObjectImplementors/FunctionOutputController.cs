﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LanguageObject))]
public class FunctionOutputController : MonoBehaviour , ILanguageObjectListener
{

    public Renderer myBox;
    public TextMesh myText;

    public FunctionController myFunction;

    private string myStorageClass;
    private string myExitEvent;

    private int numChildren;

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        
    }

    void SwitchColors()
    {
        Color temp = myText.color;
        myText.color = myBox.material.color;
        myBox.material.color = temp;
    }

    public bool AcceptableChild( LanguageObject other )
    {
        if( other.GetComponent<SoundProducer>() != null )
        {
            return true;
        }

        return false;
    }

    public void NewParent( LanguageObject parent )
    {
        // don't care
    }

    public void ParentDisconnected( LanguageObject parent )
    {
        // don't care
    }
    
    public void NewChild( LanguageObject child )
    {
        numChildren++;
        if( numChildren == 1 )
        {
            SwitchColors();
        }
    }

    public void ChildDisconnected( LanguageObject child )
    {
        numChildren--;
        if( numChildren == 0 )
        {
            SwitchColors();
        }
    }

    public void GotChuck( ChuckInstance chuck )
    {
        myStorageClass = chuck.GetUniqueVariableName();
        myExitEvent = chuck.GetUniqueVariableName();

        chuck.RunCode( string.Format( @"
            external Event {1};
            public class {0} 
            {{
                static Gain @ myGain;
            }}

            Gain g @=> {0}.myGain;
            {0}.myGain => {2};

            {1} => now;

        ", myStorageClass, myExitEvent, myFunction.GetFunctionParentConnection() ));
    }

    public void LosingChuck(ChuckInstance chuck)
    {
        chuck.RunCode( string.Format(@"{0} =< {1};", OutputConnection(), myFunction.GetFunctionParentConnection() ) );
        chuck.BroadcastEvent( myExitEvent );
    }
    
    public string InputConnection()
    {
        return string.Format( "{0}.myGain", myStorageClass );
    }

    public string OutputConnection()
    {
        return InputConnection();
    }
    
    public string VisibleName()
    {
        return myText.text;
    }

    public void CloneYourselfFrom( LanguageObject original, LanguageObject newParent )
    {
        // nothing to copy over
    }
}