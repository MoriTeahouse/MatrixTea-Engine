// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;

namespace MatrixTea.IconTool;
public static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if(args.Length!=2){Console.Error.WriteLine("MatrixTea.IconTool <source.svg> <output-base-path>");return 2;}
        try
        {
            using var reader=XmlReader.Create(args[0],new(){DtdProcessing=DtdProcessing.Prohibit,MaxCharactersInDocument=1024*1024,XmlResolver=null});
            var svg=XElement.Load(reader);if(svg.Name.LocalName!="svg")throw new InvalidDataException("Expected an SVG root.");
            double[] view=(svg.Attribute("viewBox")?.Value??"0 0 512 512").Split(new[]{' ',','},StringSplitOptions.RemoveEmptyEntries).Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray();
            if(view.Length!=4||view.Any(v=>!double.IsFinite(v))||view[2]<=0||view[3]<=0)throw new InvalidDataException("Invalid viewBox.");
            string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllBytes(output+".png",Render(svg,view,512));
            using(var input=new MemoryStream(File.ReadAllBytes(output+".png")))
            {
                var bitmap=BitmapFrame.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                var bmp=new BmpBitmapEncoder();bmp.Frames.Add(bitmap);using var file=File.Create(output+".bmp");bmp.Save(file);
            }
            int[] sizes={16,24,32,48,64,128,256};var frames=sizes.Select(size=>Render(svg,view,size)).ToArray();
            using var writer=new BinaryWriter(File.Create(output+".ico"));writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
            uint offset=(uint)(6+16*sizes.Length);
            for(int i=0;i<sizes.Length;i++)
            {
                writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);
                writer.Write((ushort)1);writer.Write((ushort)32);writer.Write((uint)frames[i].Length);writer.Write(offset);offset+=(uint)frames[i].Length;
            }
            foreach(var frame in frames)writer.Write(frame);
            Console.WriteLine($"SVG_ICON_OK source={Path.GetFullPath(args[0])} sizes={string.Join(',',sizes)}");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
    }
    private static byte[] Render(XElement svg,double[] view,int size)
    {
        var visual=new DrawingVisual();
        using(var context=visual.RenderOpen())
        {
            double scale=Math.Min(size/view[2],size/view[3]);
            context.PushTransform(new TranslateTransform((size-view[2]*scale)/2,(size-view[3]*scale)/2));
            context.PushTransform(new ScaleTransform(scale,scale));context.PushTransform(new TranslateTransform(-view[0],-view[1]));
            foreach(var element in svg.Elements())Draw(context,element);
        }
        var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();
    }
    private static void Draw(DrawingContext context,XElement element)
    {
        string name=element.Name.LocalName;
        if(name is "title" or "desc" or "metadata")return;
        if(element.Attribute("transform")!=null)throw new InvalidDataException("Transforms are not supported by this branding converter.");
        double opacity=Number(element,"opacity",1);if(opacity<0||opacity>1)throw new InvalidDataException("Invalid opacity.");
        Brush? fill=Brush(element.Attribute("fill")?.Value??"#000000");Brush? stroke=Brush(element.Attribute("stroke")?.Value??"none");
        var pen=stroke==null?null:new Pen(stroke,Number(element,"stroke-width",1));
        if(pen!=null&&element.Attribute("stroke-linecap")?.Value=="round")pen.StartLineCap=pen.EndLineCap=PenLineCap.Round;
        context.PushOpacity(opacity);
        switch(name)
        {
            case "rect":context.DrawRoundedRectangle(fill,pen,new(Number(element,"x"),Number(element,"y"),Number(element,"width"),Number(element,"height")),Number(element,"rx"),Number(element,"ry",Number(element,"rx")));break;
            case "circle":context.DrawEllipse(fill,pen,new(Number(element,"cx"),Number(element,"cy")),Number(element,"r"),Number(element,"r"));break;
            case "ellipse":context.DrawEllipse(fill,pen,new(Number(element,"cx"),Number(element,"cy")),Number(element,"rx"),Number(element,"ry"));break;
            case "path":context.DrawGeometry(fill,pen,Geometry.Parse(element.Attribute("d")?.Value??throw new InvalidDataException("Missing path geometry.")));break;
            default:throw new InvalidDataException("Unsupported branding SVG element: "+name);
        }
        context.Pop();
    }
    private static double Number(XElement element,string name,double fallback=0)=>element.Attribute(name)==null?fallback:double.Parse(element.Attribute(name)!.Value,CultureInfo.InvariantCulture);
    private static Brush? Brush(string value)=>value=="none"?null:new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
}
