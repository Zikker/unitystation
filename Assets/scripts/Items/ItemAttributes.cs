using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ItemAttributes : NetworkBehaviour
{
    private static DmiIconData dmi;
    private static DmObjectData dm;
    private static string[] hierList = { };

    public ClothEnum cloth;
    
    [SyncVar(hook = "ConstructItem")]
    public string hierarchy;
    //the bare minimum you need to to make magic work

    // item name and description.
    public string itemName;
    //dm "name"
    public string itemDescription;
    //dm "desc"

    public SpriteType spriteType;
    public ItemType type;

    //reference numbers for item on inhands spritesheet. should be one corresponding to player facing down
    public int inHandReferenceRight;
    public int inHandReferenceLeft;
    public int clothingReference = -1;

    public ItemSize size;
    //dm "w_class";

    //		dm datafile info
    private string hier;
    private Dictionary<string, string> dmDic;
    private SpriteType masterType;
    private ItemType itemType = ItemType.None;
    private DmiIcon inventoryIcon;
    private string[] invSheetPaths;
    private new string name;
    private string icon;
    private string desc;
    private string icon_state;
    private string item_color;
    private string item_state;
    private int inHandLeft = -1;
    private int inHandRight = -1;
    private int clothingOffset = -1;

    void OnStartClient()
    {
        ConstructItem(hierarchy);
    }
    
    
//    Enum test:
//    
//    private void OnEnable()
//    {
//        if (hierarchy == null || hierarchy.Equals(""))
//        {
//            hierarchy = cloth.GetDescription();
//        }
//        ConstructItem(hierarchy);
//    }

    public void ConstructItem(string hierString)
    {
        //randomize clothing! uncomment only if you spawn without any clothes on!
        //        randomizeClothHierIfEmpty();

        //don't do anything if hierarchy string is empty
        hier = hierString.Trim();
        if (hier.Length == 0)
            return;

        //init datafiles
        if (!dmi)
        {
            //				Debug.Log("Item DMI data loading...");
            dmi = Resources.Load("DmiIconData") as DmiIconData;
        }
        if (!dm)
        {
            //				Debug.Log("Item DM data loading...");
            dm = Resources.Load("DmObjectData") as DmObjectData;
        }

        //raw dictionary of attributes
        dmDic = dm.getObject(hier);

        //basic attributes
        name = tryGetAttr("name");
        desc = tryGetAttr("desc");

        //custom inventory(?) icon, if present
        icon = tryGetAttr("icon");

        //			states
        icon_state = tryGetAttr("icon_state");
        item_color = tryGetAttr("item_color"); //also a state
        item_state = tryGetAttr("item_state");
        var states = new[] { icon_state, item_color, item_state };

        masterType = getMasterType(hier);// aka SpriteType
        itemType = getItemType(hier, getInvIconPrefix(masterType));
        invSheetPaths = getItemClothSheetHier(itemType);
        //			size = getItemSize(tryGetAttr("w_class"));
        int[] inHandOffsets = tryGetInHand();
        inHandLeft = inHandOffsets[0];
        inHandRight = inHandOffsets[1];
        inventoryIcon = tryGetInventoryIcon();
        clothingOffset = tryGetClothingOffset(states);

        //determine item type via sheet name if hier name failed
        if (itemType == ItemType.None)
        {
            itemType = getItemType(inventoryIcon.getName());
        }

        //inventory item sprite
        Sprite stateSprite = tryGetStateSprite(inventoryIcon, icon_state);

        //finally setting things
        inHandReferenceLeft = inHandLeft;
        inHandReferenceRight = inHandRight;
        clothingReference = clothingOffset;
        type = itemType;
        itemName = name;
        itemDescription = desc;
        GetComponentInChildren<SpriteRenderer>().sprite = stateSprite;

        //			Debug.Log(name + " size=" + size + " type=" + type + " spriteType=" 
        //			          + spriteType + " (" + desc + ") : " 
        //			          + icon_state + " / " + item_state + " / C: " + clothingReference 
        //			          + ", L: " + inHandReferenceLeft + ", R: " + inHandReferenceRight + ", I: " + inventoryIcon.icon + '\n'
        //			          +	dmDic.Keys.Aggregate("", (current, key) => current + (key + ": ") + dmDic[key] + "\n"));
    }

    private static Sprite tryGetStateSprite(DmiIcon dmiIcon, string icon_state)
    {
        if (dmiIcon == null || dmiIcon.getName().Equals(""))
        {
            Debug.LogErrorFormat("DmiIcon '{0}' is null, unable to get state '{1}'", dmiIcon, icon_state);
            return new Sprite();
        }

        DmiState iState = dmiIcon.getState(icon_state);
        if (!iState.state.Equals(""))
        {
            return dmiIcon.spriteSheet[iState.offset];
        }
        Debug.LogErrorFormat("Failed to find inventory sprite '{1}' in icon '{0}'", dmiIcon.icon, icon_state);
        return new Sprite();
    }

    string getItemDebugInfo()
    {
        return string.Format("name={0}, type={1}, spriteType={2} ({3}) : {4} / {5} / C: {6}, L: {7}, R: {8}, I: {9}" + '\n'
        + dmDic.Keys.Aggregate("", (current, key) => current + (key + ": ") + dmDic[key] + "\n"),
            name, itemType, spriteType,
            desc, icon_state, item_state,
            clothingReference, inHandLeft, inHandRight,
            inventoryIcon.icon);
    }

    private static SpriteType getMasterType(string hs)
    {
        if (hs.StartsWith(ObjItemClothing))
            return SpriteType.Clothing;
        return SpriteType.Items;
    }

    private static string getMasterTypeHandsString(SpriteType masterType)
    {
        switch (masterType)
        {
            case SpriteType.Clothing:
                return "clothing";
            default:
                return "items";
        }
    }

    private string tryGetAttr(string key)
    {
        return tryGetAttr(dmDic, key);
    }

    public static string tryGetAttr(Dictionary<string, string> dmDic, string key)
    {
        if (dmDic != null && dmDic.ContainsKey(key))
        {
            return dmDic[key];
        }
        //			Debug.Log("tryGetAttr fail using key: " + key);
        return "";
    }

    public bool hasDataLoaded()
    {
        return dm != null && dmi != null;
    }

    private /*static*/ DmiIcon tryGetInventoryIcon(/*DmiIconData dmi, string[] invSheetPaths, string icon = ""*/)
    {
        //determining invIcon
        for (int i = 0; i < invSheetPaths.Length; i++)
        {
            var iconPath = DmiIconData.getIconPath(invSheetPaths[i]); //add extension junk
            if (!iconPath.Equals("") && dmi.Data.ContainsKey(iconPath) && icon.Equals(""))
            {
                //					Debug.Log(name + ": iSheet = dmi.DataHier[" + iconPath +"] = " + dmi.Data[iconPath]);
                return dmi.Data[iconPath];
            }
        }

        if (!icon.Equals(""))
        {
            //				Debug.Log(name + ": iSheet = dmi.DataIcon["+icon+"] = "+iSheet);
            return dmi.Data[icon];
        }
        //pretty bad choice, should use this only as last resort as it's usually pretty inaccurate
        var invIcon = dmi.getIconByState(icon_state);
        if (invIcon != null)
        {
            Debug.LogWarningFormat("{0} is doing bad dmi.getIconByState({1}) = {2}", name, icon_state, invIcon.icon);
            return invIcon;
        }
        //			Debug.LogError();
        return new DmiIcon();
    }

    //getting stuff from whatever states provided (expected order is item_state, item_color, icon_state)
    private /*static*/ int tryGetClothingOffset(string[] states)
    {
        var onPlayerClothSheetHier = getOnPlayerClothSheetHier(itemType);
        for (int i = 0; i < states.Length; i++)
        {
            if (!states[i].Equals(""))
            {
                var state = dmi.searchStateInIcon(states[i], itemType == ItemType.None ? onPlayer : onPlayerClothSheetHier, 4, false);
                if (state != null)
                {
                    return state.offset;
                }
            }
        }

        Debug.LogError("No clothing offset found!  ClothHier=" + onPlayerClothSheetHier[0] + ", " + getItemDebugInfo());
        return -1;
    }

    private /*static*/ int[] tryGetInHand()
    {
        if (item_state.Equals(""))
            return new[] { -1, -1 };

        var stateLH = dmi.searchStateInIconShallow(item_state,
                                "mob/inhands/" + getMasterTypeHandsString(masterType) + "_lefthand");

        var stateRH = dmi.searchStateInIconShallow(item_state,
                                "mob/inhands/" + getMasterTypeHandsString(masterType) + "_righthand");

        return new[] {stateLH == null ? -1 : stateLH.offset,
            stateRH == null ? -1 : stateRH.offset
        };
    }

    private static string getInvIconPrefix(SpriteType st)
    {
        switch (st)
        {
            case SpriteType.Clothing:
                return ObjItemClothing;
            default:
                return "";
        }
    }

    private static string[] getItemClothSheetHier(ItemType type)
    {
        var p = "obj/clothing/";
        switch (type)
        {
            case ItemType.Belt:
                return new[] { p + "belts" };
            case ItemType.Back:
                return new[] { p + "cloaks" };
            case ItemType.Glasses:
                return new[] { p + "glasses" };
            case ItemType.Gloves:
                return new[] { p + "gloves" };
            case ItemType.Hat:
                return new[] { p + "hats" };
            case ItemType.Mask:
                return new[] { p + "masks" };
            case ItemType.Shoes:
                return new[] { p + "shoes" };
            case ItemType.Suit:
                return new[] { p + "suits" };
            case ItemType.Neck:
                return new[] {
                    p + "ties",
                    p + "neck"
                };
            case ItemType.Uniform:
                return new[] { p + "uniforms" };
            default:
                return new[] { "" };
        }
    }

    private static string[] getOnPlayerClothSheetHier(ItemType type)
    {
        var p = "mob/";
        switch (type)
        {
            case ItemType.Belt:
                return new[] {
                    p + "belt",
                    p + "belt_mirror"
                };
            case ItemType.Back:
                return new[] { p + "back" };
            case ItemType.Glasses:
                return new[] { p + "eyes" };
            case ItemType.Gloves:
                return new[] { p + "hands" };
            case ItemType.Hat:
                return new[] { p + "head" };
            case ItemType.Ear:
                return new[] { p + "ears" };
            case ItemType.Mask:
                return new[] { p + "mask" };
            case ItemType.Shoes:
                return new[] { p + "feet" };
            case ItemType.Suit:
                return new[] { p + "suit" };
            case ItemType.Neck:
                return new[] {
                    p + "ties",
                    p + "neck"
                };
            case ItemType.Uniform:
                return new[] { p + "uniform" };
            default:
                return new[] { "" };
        }
    }

    //on-player references
    private static readonly string[] onPlayer = {
      "mob/uniform",
      "mob/underwear",
      "mob/ties",
      "mob/back",
      "mob/belt_mirror",
      "mob/belt",
      "mob/eyes",
      "mob/ears",
      "mob/hands",
      "mob/feet",
      "mob/head",
      "mob/mask",
      "mob/neck",
      "mob/suit"
    };

    private const string ObjItemClothing = "/obj/item/clothing";

    private /*static*/ void randomizeClothHierIfEmpty()
    {
        if (hierList.Length == 0)
        {
            var asset = Resources.Load(Path.Combine("metadata", "hier")) as TextAsset;
            if (asset != null)
            {
                var objects = asset.text.Split('\n').ToList();
                objects.RemoveAll(x => !x.Contains("cloth"));
                hierList = objects.ToArray();
            }
            //        Debug.Log("HierList loaded. size=" + hierList.Length);
        }
        if (hierarchy.Length == 0 && spriteType == SpriteType.Clothing)
        {
            hierarchy = hierList[new System.Random().Next(hierList.Length)];
        }
    }

    private static ItemType getItemType(string s, string cutOff = "")
    {
        //			Debug.Log("getItemType for "+ s);
        string sCut;
        if (!cutOff.Equals("") && s.StartsWith(cutOff))
        {

            sCut = s.Substring(cutOff.Length + 1).Split('/')[0];
            //				Debug.Log("sCut = "+ sCut);
        }
        else
        {
            sCut = s;
        }
        switch (sCut)
        {
            case "uniform":
            case "uniforms":
            case "under":
            case "underwear": return ItemType.Uniform;
            case "back":
            case "cloaks": return ItemType.Back;
            case "belt_mirror":
            case "belt":
            case "belts": return ItemType.Belt;
            case "eyes":
            case "glasses": return ItemType.Glasses;
            case "ears": return ItemType.Ear;
            case "gloves":
            case "hands": return ItemType.Gloves;
            case "shoes":
            case "feet": return ItemType.Shoes;
            case "head":
            case "hats": return ItemType.Hat;
            case "mask":
            case "masks": return ItemType.Mask;
            case "tie":
            case "ties":
            case "neck": return ItemType.Neck;
            case "suit":
            case "flightsuit":
            case "suits": return ItemType.Suit;
            default: return ItemType.None;
        }
    }

    private static ItemSize getItemSize(string s)
    {
        switch (s)
        {
            case "WEIGHT_CLASS_TINY": return ItemSize.Tiny;
            case "WEIGHT_CLASS_SMALL": return ItemSize.Small;
            case "WEIGHT_CLASS_NORMAL": return ItemSize.Medium;
            case "WEIGHT_CLASS_BULKY": return ItemSize.Large;
            case "WEIGHT_CLASS_HUGE": return ItemSize.Huge;
            default: return ItemSize.Small;
        }
    }

    //Below methods add a code to the start of the sprite reference to indicate which spritesheet to use:
    //1 = items
    //2 = clothing
    //3 = guns
    public int NetworkInHandRefLeft()
    {
        if (inHandReferenceLeft == -1)
            return -1;

        string code = SpriteTypeCode();
        string newRef = code + inHandReferenceLeft.ToString();
        int i = -1;
        int.TryParse(newRef, out i);
        return i;
    }

    public int NetworkInHandRefRight()
    {
        if (inHandReferenceRight == -1)
            return -1;

        string code = SpriteTypeCode();
        string newRef = code + inHandReferenceRight.ToString();
        int i = -1;
        int.TryParse(newRef, out i);
        return i;
    }

    private string SpriteTypeCode()
    {
        int i = -1;
        switch (spriteType)
        {
            case SpriteType.Items:
                i = 1;
                break;
            case SpriteType.Clothing:
                i = 2;
                break;
            case SpriteType.Guns:
                i = 3;
                break;
        }
        return i.ToString();
    }

    public void OnMouseEnter()
    {
        UI.UIManager.SetToolTip = this.itemName + " (" + this.itemDescription + ")";
    }

    public void OnMouseExit()
    {
        UI.UIManager.SetToolTip = "";
    }

    // Sends examine event to all monobehaviors on gameobject
    public void SendExamine()
    {
        SendMessage("OnExamine");
    }

    // When right clicking on an item, examine the item
    public void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1))
        {
            SendExamine();
        }
    }

    public void OnExamine()
    {
        if (!String.IsNullOrEmpty(itemDescription))
            UI.UIManager.Chat.AddChatEvent(new ChatEvent(itemDescription));
    }
}