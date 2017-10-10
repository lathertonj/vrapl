﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LanguageObject))]
[RequireComponent(typeof(SoundProducer))]
public class FunctionInputController : MonoBehaviour , ILanguageObjectListener
{
    public FunctionController myFunction;

    public Renderer myBox;
    public TextMesh myText;

    private string myStorageClass;
    private string myExitEvent;
    
    private ILanguageObjectListener myParent;

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
        return false;
    }

    public void NewParent( LanguageObject parent )
    {
        ILanguageObjectListener newParent = (ILanguageObjectListener) parent.GetComponent(typeof(ILanguageObjectListener));
        if( newParent != null )
        {
            SwitchColors();
            myParent = newParent;
        }
        
    }

    public void ParentDisconnected( LanguageObject parent )
    {
        ILanguageObjectListener losingParent = (ILanguageObjectListener) parent.GetComponent(typeof(ILanguageObjectListener));
        if( losingParent == myParent )
        {
            SwitchColors();
            myParent = null;
        }
    }
    
    public void NewChild( LanguageObject child )
    {
        // don't care
    }

    public void ChildDisconnected( LanguageObject child )
    {
        // don't care
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

        ", myStorageClass, myExitEvent, myParent.InputConnection() ));

        myFunction.TellUgenChildrenGotChuck( chuck );
    }

    public void LosingChuck( ChuckInstance chuck )
    {
        myFunction.TellUgenChildrenLosingChuck( chuck );
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