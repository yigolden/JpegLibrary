namespace JpegLibrary.Exp
{
    public readonly struct JpegScanComponentEncodingSpecification
    {
        public byte ComponentIndex { get; }
        public byte DcTableIdentifier { get; }
        public byte AcTableIdentifier { get; }
        public bool GenerateDcTable { get; }
        public bool GenerateAcTable { get; }

        public JpegScanComponentEncodingSpecification(byte componentIndex, byte dcTableIdentifier, byte acTableIdentifier)
        {
            ComponentIndex = componentIndex;
            DcTableIdentifier = dcTableIdentifier;
            AcTableIdentifier = acTableIdentifier;
            GenerateDcTable = false;
            GenerateAcTable = false;
        }

        public JpegScanComponentEncodingSpecification(byte componentIndex, byte dcTableIdentifier, byte acTableIdentifier, bool generateDcTable, bool generateAcTable)
        {
            ComponentIndex = componentIndex;
            DcTableIdentifier = dcTableIdentifier;
            AcTableIdentifier = acTableIdentifier;
            GenerateDcTable = generateDcTable;
            GenerateAcTable = generateAcTable;
        }
    }
}
