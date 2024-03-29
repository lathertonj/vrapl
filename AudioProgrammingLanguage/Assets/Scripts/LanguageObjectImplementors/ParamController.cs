﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LanguageObject))]
public class ParamController : MonoBehaviour , ILanguageObjectListener, IControllerInputAcceptor
{
    public GameObject myText;
    public GameObject myShape;

    private string myStorageClass;
    private string myExitEvent;
    private int myNumChildren = 0;
    private string myParam;
    private int myParamIndex = 0;
    private bool amConnected = false;
    private ChuckSubInstance myChuck = null;
    private LanguageObject myLO;

    private ILanguageObjectListener myParent = null;
    private IParamAcceptor myParamAcceptor = null;

    public void InitLanguageObject( ChuckSubInstance chuck )
    {
        myLO = GetComponent<LanguageObject>();

        // init chuck
        myChuck = chuck;

        myStorageClass = chuck.GetUniqueVariableName();
        myExitEvent = chuck.GetUniqueVariableName();

        chuck.RunCode(string.Format(@"
            global Event {1};
            public class {0}
            {{
                static Gain @ myGain;
            }}
            Gain g @=> {0}.myGain;
            {0}.myGain => blackhole;

            // wait until told to exit
            {1} => now;
        ", myStorageClass, myExitEvent));
    }

    public void CleanupLanguageObject( ChuckSubInstance chuck )
    {
        chuck.BroadcastEvent( myExitEvent );
        myChuck = null;
    }
	
	private void SwitchColors()
    {
        Color tempColor = myText.GetComponent<TextMesh>().color;
        myText.GetComponent<TextMesh>().color = myShape.GetComponent<Renderer>().material.color;
        myShape.GetComponent<Renderer>().material.color = tempColor;
    }

    public void ParentConnected( LanguageObject parent, ILanguageObjectListener parentListener )
    {
        IParamAcceptor parentParamAcceptor = (IParamAcceptor) parent.GetComponent( typeof(IParamAcceptor) );
        if( parentParamAcceptor != null )
        {
            myParamAcceptor = parentParamAcceptor;
            myParamIndex = 0;
            myParam = myParamAcceptor.AcceptableParams()[myParamIndex];
            myText.GetComponent<TextMesh>().text = myParam;
            SwitchColors();
            myParent = parentListener;

            if( myNumChildren > 0 )
            {
                // if I already have some children when I get a parent, I need to hook myself up
                myParamAcceptor.ConnectParam( myParam, OutputConnection() );
                amConnected = true;
            }
        }
    }

    public void ParentDisconnected( LanguageObject parent, ILanguageObjectListener parentListener )
    {
        if( myParamAcceptor != null )
        {
            if( amConnected )
            {
                // if I am still connected when I lose my parent, I need to disconnect myself
                myParamAcceptor.DisconnectParam( myParam, OutputConnection() );
                amConnected = false;
            }

            SwitchColors();
            myParam = "param";
            myText.GetComponent<TextMesh>().text = myParam;
            myParamAcceptor = null;
            myParent = null;
        }
    }

    public bool AcceptableChild( LanguageObject other, ILanguageObjectListener otherListener )
    {
        // todo: accept others?
        if( other.GetComponent<NumberProducer>() != null ||
            other.GetComponent<SoundProducer>() != null )
        {
            return true;
        }

        return false;
    }


    public void ChildConnected( LanguageObject child, ILanguageObjectListener childListener )
    {
        LanguageObject.HookTogetherLanguageObjects( myChuck, child, myLO );

        myNumChildren++;
        // if I got my first child after I was already hooked up to a chuck,
        // I need to hook up myself now.
        if( myNumChildren == 1 && myParamAcceptor != null && myChuck != null )
        {
            myParamAcceptor.ConnectParam( myParam, OutputConnection() );
            amConnected = true;
        }
    }
    
    public void ChildDisconnected( LanguageObject child, ILanguageObjectListener childListener )
    {
        LanguageObject.UnhookLanguageObjects( myChuck, child, myLO );

        myNumChildren--;
        // if I lost my last child while I was hooked up to a chuck,
        // I need to disconnect myself now
        if( myNumChildren == 0 && myParamAcceptor != null && myChuck != null )
        {
            myParamAcceptor.DisconnectParam( myParam, OutputConnection() );
            amConnected = false;
        }
    }

    public string InputConnection( LanguageObject whoAsking )
    {
        return OutputConnection();
    }

    public string OutputConnection()
    {
        return string.Format("{0}.myGain", myStorageClass);
    }

    public void SizeChanged( float newSize )
    {
        // don't care about my size
    }

    public string GetParamName()
    {
        return string.Format( "{0}'s {1}", myParent.VisibleName(), myParam );
    }

    public void ResetAcceptableParams()
    {
        // only disconnect and reconnect if I was already connected!
        bool shouldDisconnectAndReconnect = amConnected;
        if( shouldDisconnectAndReconnect ) myParamAcceptor.DisconnectParam( myParam, OutputConnection() );

        // try to get the same index, but not if it's no longer a valid index
        myParamIndex %= myParamAcceptor.AcceptableParams().Length;
        myParam = myParamAcceptor.AcceptableParams()[myParamIndex];
        myText.GetComponent<TextMesh>().text = myParam;

        // if I was already connected before, I need to connect this new param
        if( shouldDisconnectAndReconnect ) myParamAcceptor.ConnectParam( myParam, OutputConnection() );
    }

    public void TouchpadDown()
    {
        if( myParamAcceptor != null )
        {
            // only disconnect and reconnect if I was already connected!
            bool shouldDisconnectAndReconnect = amConnected;
            if( shouldDisconnectAndReconnect ) myParamAcceptor.DisconnectParam( myParam, OutputConnection() );

            // change my param and my text
            myParamIndex++;
            myParamIndex %= myParamAcceptor.AcceptableParams().Length;
            myParam = myParamAcceptor.AcceptableParams()[myParamIndex];
            myText.GetComponent<TextMesh>().text = myParam;

            // if I was already connected before, I need to connect this new param
            if( shouldDisconnectAndReconnect ) myParamAcceptor.ConnectParam( myParam, OutputConnection() );
        }
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
    
    public string VisibleName()
    {
        return myText.GetComponent<TextMesh>().text;
    }

    public void CloneYourselfFrom( LanguageObject original, LanguageObject newParent )
    {
        ParamController other = original.GetComponent< ParamController >();

        // simulate touchpad presses until our state matches
        while( myParamIndex != other.myParamIndex )
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
        // no int params
        return new int[] { myParamIndex };
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
        // simulate touchpad presses until param index matches
        while( myParamIndex != intParams[0] )
        {
            TouchpadDown();
        }
    }
}
