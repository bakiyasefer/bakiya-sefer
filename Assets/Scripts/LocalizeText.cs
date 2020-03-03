using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using FullInspector;

public class LocalizeText : BaseBehavior<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    public string term = string.Empty;
    Text text = null;
    GameController.GetValue<object[]> formatValues = null;
    
    public void SetTerm(string trm)
    {
        SetTerm(trm, null);
    }
    public void SetTerm(string trm, GameController.GetValue<object[]> values)
    {
        term = trm;
        formatValues = values;
        
        Localize();
    }

    public void Localize()
    {
        if (!text || !enabled) return;
        //get term text value
        string localizedValue = GameController.Instance.Localize(term);
        text.text = formatValues != null ? string.Format(localizedValue, formatValues()) : localizedValue;
    }

    void Start()
    {
        text = GetComponent<Text>();
        GameController.Instance.AddOnLangChanged(Localize);
        Localize();
    }

    void OnEnable()
    {
        Localize();
    }

    void OnDestroy()
    {
        var gc = GameController.Instance;
        if(gc != null) gc.RemoveOnLangChanged(Localize);
    }
}
