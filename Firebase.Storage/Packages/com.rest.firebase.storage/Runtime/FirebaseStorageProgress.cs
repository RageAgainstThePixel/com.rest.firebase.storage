// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Firebase.Storage
{
    public struct FirebaseStorageProgress
    {
        public FirebaseStorageProgress(long position, long length, float speed, string unit)
        {
            Position = position;
            Length = length;
            Percentage = (int)((position / (double)length) * 100);
            Speed = speed;
            Unit = unit;
            AvgSpeed = $"{Speed} {Unit}/s";
        }

        public long Length { get; }

        public int Percentage { get; }

        public long Position { get; }

        public float Speed { get; }

        public string AvgSpeed { get; }

        public string Unit { get; }
    }
}
