// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Firebase.Storage
{
    public struct FirebaseStorageProgress
    {
        public FirebaseStorageProgress(long position, long length)
        {
            Position = position;
            Length = length;
            Percentage = (int)((position / (double)length) * 100);
        }

        public long Length { get; }

        public int Percentage { get; }

        public long Position { get; }
    }
}
