using Microsoft.EntityFrameworkCore;
using TTH.Areas.Super.Data;
using TTH.Areas.Super.Data.Rent;
using TTH.Areas.Super.Models.Rent;

namespace TTH.Areas.Super.Repository.RentRepository
{
    public class SizeRepository
    {
        private readonly AppDataContext _context;

        public SizeRepository(AppDataContext context)
        {
            _context = context;
        }

        public async Task<List<SizeModel>> GetSize()
        {
            return await _context.Size.Select(x => new SizeModel()
            {
                IdSize = x.IdSize,
                Name = x.Name,
            }).ToListAsync();
        }

        public async Task AddSize(SizeModel sizeModel)
        {
            var size = new Size
            {
                Name = sizeModel.Name
            };
            _context.Size.Add(size);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteSize(int id)
        {
            var size = await _context.Size.FindAsync(id);
            if (size != null)
            {
                _context.Size.Remove(size);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<SizeModel> GetSizeById(int id)
        {
            return await _context.Size.Where(x => x.IdSize == id).Select(x => new SizeModel()
            {
                IdSize = x.IdSize,
                Name = x.Name,
            }).FirstOrDefaultAsync();
        }
    }
}