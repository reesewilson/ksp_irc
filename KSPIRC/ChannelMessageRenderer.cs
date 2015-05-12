﻿/*
KSPIRC - Internet Relay Chat plugin for Kerbal Space Program.
Copyright (C) 2013 Maik Schreiber

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KSPIRC
{
    internal abstract class ChannelMessageRenderer
    {
        protected readonly string sender;
        protected readonly string sourceText;

        internal ChannelMessageRenderer(string sender, string text)
        {
            this.sender = sender;
            this.sourceText = text;
        }

        abstract internal float SenderWidth(ChannelGUI gui);

        abstract internal void Render(ChannelGUI gui, float spaceWidth, float maxNameWidth);

        abstract internal void OnWindowResized();
    }

    internal class ChannelMessageRendererFactory
    {
        internal ChannelMessageRendererFactory()
        {

        }

        internal ChannelMessageRenderer get(ChannelGUI gui, string sender, string text)
        {
            //return new MultiLabelChannelMessageRenderer(sender, text);
            return new RichTextLabelChannelMessageRenderer(sender, text);
        }

        private class MultiLabelChannelMessageRenderer : ChannelMessageRenderer
        {
            private class Element
            {
                private string text;
                private readonly bool link;

                internal Element(string text, bool link)
                {
                    this.text = text;
                    this.link = link;
                }

                internal void Append(string word)
                {
                    this.text += " " + word;
                }

                internal float RenderWidth(ChannelGUI gui, string sender)
                {
                    bool highlighted = gui.highlightName && (sender != "*") && text.ToLower().Contains(gui.config.nick.ToLower());

                    GUIStyle style = highlighted ? gui.textHighlightedStyle : gui.textStyle;

                    return style.CalcSize(new GUIContent(text)).x;
                }

                internal void Render(ChannelGUI gui, string sender)
                {
                    bool highlighted = gui.highlightName && (sender != "*") && text.ToLower().Contains(gui.config.nick.ToLower());

                    GUIStyle style = highlighted ? gui.textHighlightedStyle : gui.textStyle;

                    if (link)
                    {
                        if (GUILayout.Button(text, gui.textLinkStyle))
                        {
                            handleClick(gui);
                        }
                    }
                    else
                    {
                        GUILayout.Label(text, style);
                    }
                }

                private void handleClick(ChannelGUI gui)
                {
                    gui.linkWindow.load(text);
                }
            }

            private static bool appendNonLinks = false;

            private float sourceTextWidth = -1;
            private readonly Element[] elements;

            internal MultiLabelChannelMessageRenderer(string sender, string text)
                : base(sender, text)
            {
                List<Element> tempElements = new List<Element>();

                string[] words = text.Split(' ');
                Element lastElement = null;
                foreach (string word in words)
                {
                    string lower = word.ToLower();
                    bool isLink = (lower.Contains("http://") || lower.Contains("https://"));

                    if (appendNonLinks)
                    {
                        if (isLink || lastElement == null)
                        {
                            Element element = new Element(word, isLink);

                            tempElements.Add(element);
                            lastElement = (isLink ? null : element);
                        }
                        else
                        {
                            lastElement.Append(word);
                        }
                    }
                    else
                    {
                        Element element = new Element(word, isLink);

                        tempElements.Add(element);
                    }
                }

                this.elements = tempElements.ToArray();
            }

            internal override float SenderWidth(ChannelGUI gui)
            {
                return gui.senderStyle.CalcSize(new GUIContent(sender)).x;
            }

            internal override void OnWindowResized()
            {
                sourceTextWidth = -1.0f;
            }

            private static float fudgeFactor = 60.0f;

            internal override void Render(ChannelGUI gui, float spaceWidth, float maxNameWidth)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(sender, gui.senderStyle, GUILayout.Width(maxNameWidth), GUILayout.MaxWidth(maxNameWidth));
                if (gui.config.debug)
                {
                    GUILayout.Label(new GUIContent("" + sourceTextWidth), gui.senderStyle);
                }

                GUILayout.BeginVertical();
                if (gui.config.forceSimpleRender || sourceTextWidth < 0)
                {
                    startLine();
                    GUILayout.Label(sourceText, gui.textStyle);
                    endLine();
                }
                else
                {
                    RenderElements(gui, spaceWidth, sourceTextWidth);
                }
                GUILayout.EndVertical();
                if (Event.current.type == EventType.Repaint)
                {
                    sourceTextWidth = GUILayoutUtility.GetLastRect().width - fudgeFactor;
                }
                GUILayout.EndHorizontal();
            }

            private float RenderElements(ChannelGUI gui, float spaceWidth, float maxWidth)
            {
                float longestLineWidth = 0;
                float lineWidth = 0;
                bool firstElement = true;
                startLine();
                foreach (Element element in elements)
                {
                    float elementWidth = element.RenderWidth(gui, sender);
                    if (!firstElement && lineWidth + elementWidth > maxWidth)
                    {
                        endLine();
                        startLine();
                        lineWidth = elementWidth;
                    }
                    else
                    {
                        lineWidth += elementWidth;
                    }
                    element.Render(gui, sender);

                    GUILayout.Space(spaceWidth);
                    firstElement = false;
                    if (lineWidth > longestLineWidth)
                    {
                        longestLineWidth = lineWidth;
                    }
                }
                endLine();

                return longestLineWidth;
            }

            private void startLine()
            {
                GUILayout.BeginHorizontal();
            }

            private void endLine()
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private class RichTextLabelChannelMessageRenderer : ChannelMessageRenderer
        {
            private readonly string senderRichText;
            private readonly string textRichText;

            private bool stylesInitialized = false;
            private GUIStyle senderTextStyle;
            private GUIStyle textStyle;
            private GUIStyle textHighlightedStyle;
            private float senderWidth;

            private Rect textRect;

            internal RichTextLabelChannelMessageRenderer(string sender, string text)
                : base(sender, text)
            {
                this.senderRichText = ToRichText(sender);
                this.textRichText = ToRichText(text);
            }


            internal override float SenderWidth(ChannelGUI gui)
            {
                initStyles(gui);
                return senderWidth;
            }

            internal override void Render(ChannelGUI gui, float spaceWidth, float maxNameWidth)
            {
                initStyles(gui);

                bool highlighted = gui.highlightName && (sender != "*") && this.sourceText.ToLower().Contains(gui.config.nick.ToLower());

                GUIStyle style = (highlighted ? textHighlightedStyle : textStyle);

                GUILayout.BeginHorizontal();
                GUILayout.Label(senderRichText, senderTextStyle, GUILayout.Width(maxNameWidth), GUILayout.MaxWidth(maxNameWidth));
                if (GUILayout.Button(textRichText, style))
                {
                    int textIndex = style.GetCursorStringIndex(textRect, new GUIContent(textRichText), Event.current.mousePosition);
                    handleClick(gui, textRichText, textIndex);
                }
                if (Event.current.type == EventType.Repaint)
                {
                    textRect = GUILayoutUtility.GetLastRect();
                }
                GUILayout.EndHorizontal();
            }

            internal override void OnWindowResized()
            {

            }

            private void handleClick(ChannelGUI gui, string text, int index)
            {
                string word = GetWordAt(text, index);
                if (word.StartsWith("http:") || word.StartsWith("https:"))
                {
                    gui.linkWindow.load(word);
                }
            }

            private string GetWordAt(string text, int index)
            {
                // find the start of the word
                int wordStartIndex = 0;
                for (int ix = index - 1; ix >= 0; ix--)
                {
                    char aChar = text[ix];
                    if (aChar == ' ' || aChar == '>')
                    {
                        wordStartIndex = ix + 1;
                        break;
                    }
                }

                int wordEndIndex = text.Length - 1;
                for (int ix = index + 1; ix < text.Length; ix++)
                {
                    char aChar = text[ix];
                    if (aChar == ' ' || aChar == '<' || aChar == ',')
                    {
                        wordEndIndex = ix - 1;
                        break;
                    }
                }

                return text.Substring(wordStartIndex, wordEndIndex - wordStartIndex + 1);
            }

            private void initStyles(ChannelGUI gui)
            {
                if (!this.stylesInitialized)
                {
                    this.senderTextStyle = new GUIStyle(gui.senderStyle);
                    this.senderTextStyle.normal.textColor = Color.white;
                    this.senderTextStyle.richText = true;

                    this.textStyle = new GUIStyle(gui.textStyle);
                    this.textStyle.normal.textColor = Color.white;
                    this.textStyle.richText = true;

                    this.textHighlightedStyle = new GUIStyle(gui.textHighlightedStyle);
                    this.textHighlightedStyle.normal.textColor = Color.white;
                    this.textHighlightedStyle.richText = true;

                    this.senderWidth = senderTextStyle.CalcSize(new GUIContent(senderRichText)).x;

                    this.stylesInitialized = true;
                }
            }

            private static string ToRichText(string text)
            {
                char[] textChars = text.ToCharArray();

                string richTextified = "";

                int index = 0;
                char textChar;

                bool inItalic = false;
                bool inBold = false;
                bool inColor = false;

                while (GetNextChar(textChars, out textChar, ref index))
                {
                    switch (textChar)
                    {
                        case '\x02':
                            // bold
                            richTextified += updateBoldItalic(inBold, inItalic, !inBold, inItalic);
                            inBold = !inBold;
                            break;

                        case '\x03':
                            // color
                            string colorTag;
                            if (ReadColorTag(textChars, out colorTag, ref index))
                            {
                                richTextified += colorTag;
                                inColor = true;
                            }
                            else if (inColor)
                            {
                                richTextified += "</color>";
                                inColor = false;
                            }
                            break;

                        case '\x0f':
                            // reset formatting
                            richTextified += updateBoldItalic(inBold, inItalic, false, false);
                            if (inColor)
                            {
                                richTextified += "</color>";
                                inColor = false;
                            }
                            break;

                        case '\x1d':
                            // italic
                            richTextified += updateBoldItalic(inBold, inItalic, inBold, !inItalic);
                            inItalic = !inItalic;
                            break;

                        case '\x1f':
                            // underlined not supported
                            break;

                        case '\t':
                            // tab
                            richTextified += "    ";
                            break;

                        case '\n':
                            // newline
                            richTextified += "\n";
                            break;

                        default:
                            // pass through
                            richTextified += textChar;
                            break;
                    }
                }

                if (inBold)
                {
                    richTextified += "</b>";
                }
                if (inItalic)
                {
                    richTextified += "</i>";
                }
                if (inColor)
                {
                    richTextified += "</color>";
                }

                return richTextified;
            }

            private static string updateBoldItalic(bool oldBold, bool oldItalic, bool newBold, bool newItalic)
            {
                Dictionary<int, string> map = new Dictionary<int, string>(16);
                //                             #  oldB oldI newB newI  preveff action
                map[00] = "";               // 0  -    -    -    -     -       -
                map[01] = "<i>";            // 1  -    -    -    x     -       <i>
                map[02] = "<b>";            // 2  -    -    x    -     -       <b>
                map[03] = "<b><i>";         // 3  -    -    x    x     -       <b><i>
                map[04] = "</i>";           // 4  -    x    -    -     <i>     </i>
                map[05] = "";               // 5  -    x    -    x     <i>     -
                map[06] = "</i><b>";        // 6  -    x    x    -     <i>     </i><b>
                map[07] = "</i><b><i>";     // 7  -    x    x    x     <i>     <b>
                map[08] = "</b>";           // 8  x    -    -    -     <b>     </b>
                map[09] = "</b><i>";        // 9  x    -    -    x     <b>     </b><i>
                map[10] = "";               // 10 x    -    x    -     <b>     -
                map[11] = "<i>";            // 11 x    -    x    x     <b>     <i>
                map[12] = "</i></b>";       // 12 x    x    -    -     <b><i>  </i></b>
                map[13] = "</i></b><i>";    // 13 x    x    -    x     <b><i>  </i></b><i>
                map[14] = "</i>";           // 14 x    x    x    -     <b><i>  </i>
                map[15] = "";               // 15 x    x    x    x     <b><i>  -

                int index = (oldBold ? 8 : 0) + (oldItalic ? 4 : 0) + (newBold ? 2 : 0) + (newItalic ? 1 : 0);

                return map[index];
            }

            private static bool ReadColorTag(char[] source, out string colorTag, ref int index)
            {
                colorTag = "";

                bool hasFgColor = false;
                bool hasBgColor = false;

                {
                    string fgColorDigits = "";
                    char fgColorChar;
                    while (GetNextChar(source, out fgColorChar, ref index))
                    {
                        if (Char.IsDigit(fgColorChar))
                        {
                            fgColorDigits += fgColorChar;
                        }
                        else if (fgColorChar == ',')
                        {
                            hasBgColor = true;
                            break;
                        }
                        else
                        {
                            index--;
                            break;
                        }
                    }

                    if (fgColorDigits.Length > 0)
                    {
                        int colorValue = Convert.ToInt32(fgColorDigits);

                        string rgb;
                        if (IRCColorToRGB(colorValue, out rgb))
                        {
                            hasFgColor = true;
                            colorTag = "<color=" + rgb + ">";
                        }
                    }
                }

                if (hasBgColor)
                {
                    // we don't support bgColor, but need to clean it out cleanly
                    char bgColorChar;
                    while (GetNextChar(source, out bgColorChar, ref index))
                    {
                        if (!Char.IsDigit(bgColorChar))
                        {
                            index--;
                            break;
                        }
                    }
                }

                return hasFgColor;
            }

            private static bool GetNextChar(char[] source, out char value, ref int index)
            {
                if (index >= source.Length)
                {
                    value = (char)0;
                    index = -1;
                    return false;
                }

                value = source[index++];
                return true;
            }

            private static bool IRCColorToRGB(int colorValue, out string rgb)
            {
                Dictionary<int, string> map = new Dictionary<int, string>(16);
                map[00] = "white";       //	White
                map[01] = "black";       //	Black
                map[02] = "navy";        //	blue (navy)
                map[03] = "green";       //	Green
                map[04] = "red";         //	Red
                map[05] = "maroon";      //	brown (maroon)
                map[06] = "purple";      //	Purple
                map[07] = "olive";       //	orange (olive)
                map[08] = "yellow";      //	Yellow
                map[09] = "lime";        //	light green (lime)
                map[10] = "teal";        //	teal (a green/blue cyan)
                map[11] = "aqua";        //	light cyan (cyan / aqua)
                map[12] = "blue";        //	light blue (royal)
                map[13] = "fuchsia";     //	pink (light purple / fuchsia)
                map[14] = "grey";        //	Grey
                map[15] = "silver";      //	light grey (silver)

                return map.TryGetValue(colorValue, out rgb);
            }
        }
    }
}