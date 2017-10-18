﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NumberProducer))]
[RequireComponent(typeof(LanguageObject))]
public class DurController : MonoBehaviour , ILanguageObjectListener , IControllerInputAcceptor
{

    public TextMesh myText;
    public MeshRenderer myShape;

    private NumberController myNumber = null;
    private int myCurrentIndex;
    private string myCurrentDurType;
    private string[] myDurTypes;

    private string myStorageClass;
    private string myExitEvent;
    private ILanguageObjectListener myParent = null;
    private ChuckInstance myChuck = null;

	// Use this for initialization
	void Awake() 
    {
		myDurTypes = new string[] { "ms", "second", "sample" };
        myCurrentIndex = 0;
        myCurrentDurType = myDurTypes[myCurrentIndex];
        myText.text = myCurrentDurType;
	}
	
	void SwitchColors()
    {
        Color temp = myText.color;
        myText.color = myShape.material.color;
        myShape.material.color = temp;
    }

    private void UpdateMyGain()
    {
        if( myChuck == null )
        {
            return;
        }
        string durName = myCurrentDurType;
        if( durName == "sample" ) durName = "samp";
        myChuck.RunCode( string.Format( "{0} / second => {1}.gain;", durName, OutputConnection() ) );
    }

    public void TouchpadDown()
    {
        myCurrentIndex++;
        myCurrentIndex %= myDurTypes.Length;
        myCurrentDurType = myDurTypes[myCurrentIndex];
        myText.text = myCurrentDurType;
        UpdateMyGain();
    }


    public void TouchpadUp()
    {
        // don't care
    }

    public void TouchpadAxis(Vector2 pos)
    {
        // don't care
    }

    public void TouchpadTransform( Transform t )
    {
        // don't care
    }

    public bool AcceptableChild( LanguageObject other )
    {
        if( myNumber == null && other.GetComponent<NumberController>() != null )
        {
            return true;
        }
        return false;
    }

    public void NewParent( LanguageObject parent )
    {
        ILanguageObjectListener lo = (ILanguageObjectListener) parent.GetComponent( typeof( ILanguageObjectListener ) );
        if( lo != null )
        {
            myParent = lo;
            SwitchColors();
        }
    }

    public void ParentDisconnected( LanguageObject parent )
    {
        ILanguageObjectListener lo = (ILanguageObjectListener) parent.GetComponent( typeof( ILanguageObjectListener ) );
        if( lo == myParent )
        {
            SwitchColors();
            myParent = null;
        }
    }

    public void NewChild( LanguageObject child )
    {
        NumberController nc = child.GetComponent<NumberController>();
        if( nc != null )
        {
            myNumber = nc;
        }
    }

    public void ChildDisconnected(LanguageObject child)
    {
        if( child.GetComponent<NumberController>() == myNumber )
        {
            myNumber = null;
        }
    }

    public string InputConnection()
    {
        return string.Format("{0}.myGain", myStorageClass);
    }

    public string OutputConnection()
    {
        return InputConnection();
    }

    public void GotChuck(ChuckInstance chuck)
    {
        myChuck = chuck;
        myStorageClass = chuck.GetUniqueVariableName();
        myExitEvent = chuck.GetUniqueVariableName();

        chuck.RunCode(string.Format(@"
            external Event {1};
            public class {0}
            {{
                static Gain @ myGain;
            }}
            Gain g @=> {0}.myGain;
            0.001 => {0}.myGain.gain;

            // wait until told to exit
            {1} => now;
        ", myStorageClass, myExitEvent ));

        UpdateMyGain();

        if( myParent != null )
        {
            chuck.RunCode(string.Format("{0} => {1};", OutputConnection(), myParent.InputConnection() ) );
        }
    }

    public void LosingChuck(ChuckInstance chuck)
    {
        if( myParent != null )
        {
            chuck.RunCode(string.Format("{0} =< {1};", OutputConnection(), myParent.InputConnection() ) );
        }

        chuck.BroadcastEvent( myExitEvent );
        myChuck = null;
    }

    public void SizeChanged( float newSize )
    {
        // don't care about my size
    }

    public string VisibleName()
    {
        return myText.text;
    }

    public void CloneYourselfFrom( LanguageObject original, LanguageObject newParent )
    {
        DurController other = original.GetComponent<DurController>();

        // simulate touchpad presses until state matches
        while( myCurrentIndex != other.myCurrentIndex )
        {
            TouchpadDown();
        }
    }

    // Serialization for storage on disk
    public string[] SerializeStringParams( int version )
    {
        // no string params
        return LanguageObject.noStringParams;
    }

    public int[] SerializeIntParams( int version )
    {
        // current index
        return new int[] { myCurrentIndex };
    }

    public float[] SerializeFloatParams( int version )
    {
        // no float params
        return LanguageObject.noFloatParams;
    }

    public object[] SerializeObjectParams( int version )
    {
        // no object params
        return LanguageObject.noObjectParams;
    }

    public void SerializeLoad( int version, string[] stringParams, int[] intParams, 
        float[] floatParams, object[] objectParams )
    {
        // simulate touchpad presses until state matches
        while( myCurrentIndex != intParams[0] )
        {
            TouchpadDown();
        }
    }
}
