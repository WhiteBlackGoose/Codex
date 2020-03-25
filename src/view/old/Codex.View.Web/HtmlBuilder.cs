using Bridge.Html5;
using Granular.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.View.Web
{
    public static class HtmlBuilder
    {
        public static readonly string NBSP = ((char)160).ToString();

        public static readonly HtmlValueConverter Converter = new HtmlValueConverter();

        public static HtmlNode<HTMLDivElement> Div(params IHtmlModifier<HTMLDivElement>[] modifiers)
        {
            var element = new HTMLDivElement();
            element.Apply(modifiers);
            return element;
        }

        public static HtmlNode<HTMLAnchorElement> Anchor(params IHtmlModifier<HTMLAnchorElement>[] modifiers)
        {
            var element = new HTMLAnchorElement();
            element.Apply(modifiers);
            return element;
        }

        public static HtmlNode<Text> Text(string value)
        {
            return new Text(value);
        }

        public static IHtmlModifier<HTMLAnchorElement> Target(string value)
        {
            return Create<HTMLAnchorElement>(e => e.Target = value);
        }

        public static IHtmlModifier<HTMLAnchorElement> Href(string value)
        {
            return Create<HTMLAnchorElement>(e => e.Href = value);
        }

        public static IHtmlModifier<HTMLElement> Title(string value)
        {
            return Create<HTMLElement>(e => e.Title = value);
        }

        public static IHtmlModifier<HTMLElement> Class(string value)
        {
            return Create<HTMLElement>(e => e.ClassName = value);
        }

        public static IHtmlModifier<HTMLElement> Id(string value)
        {
            return Create<HTMLElement>(e => e.Id = value);
        }

        public static IHtmlModifier<T> Create<T>(Action<T> apply)
        {
            return new ActionHtmlModifier<T>(apply);
        }

        public static HtmlNode<T> Append<T>(this T node) where T : Node
        {
            return node;
        }

        public static void Apply<T>(this T target, IHtmlModifier<T>[] modifiers)
        {
            foreach (var modifier in modifiers)
            {
                modifier.Apply(target);
            }
        }
    }

    public class ActionHtmlModifier<T> : IHtmlModifier<T>
    {
        private readonly Action<T> m_action;

        public ActionHtmlModifier(Action<T> action)
        {
            m_action = action;
        }

        public void Apply(T target)
        {
            m_action(target);
        }
    }

    public class HtmlNode<TNode> : IHtmlModifier<Node>
        where TNode : Node
    {
        public readonly TNode Node;

        public HtmlNode(TNode node)
        {
            Node = node;
        }

        public void Apply(Node target)
        {
            target.AppendChild(Node);
        }

        public static implicit operator HtmlNode<TNode>(TNode node)
        {
            return new HtmlNode<TNode>(node);
        }

        public static implicit operator Node(HtmlNode<TNode> node)
        {
            return node.Node;
        }
    }

    public interface IHtmlModifier<in TTarget>
    //where TTarget : HTMLElement
    {
        void Apply(TTarget target);
    }
}
