namespace JpegLibrary
{
    internal interface IJpegEncodingTableBuilder<out TTable> where TTable : class, IJpegEncodingTable
    {
        TTable Build(bool optimal);
    }
}
