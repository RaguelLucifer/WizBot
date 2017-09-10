using WizBot.Services;
using System;
using WizBot.Services.Database.Models;
using Discord;
using WizBot.Modules.Xp.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace WizBot.Modules.Xp.Services
{
    public class ClubService : INService
    {
        private readonly DbService _db;

        public ClubService(DbService db)
        {
            _db = db;
        }

        public bool CreateClub(IUser user, string clubName, out ClubInfo club)
        {
            //must be lvl 5 and must not be in a club already

            club = null;
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                uow._context.SaveChanges();
                var xp = new LevelStats(uow.Xp.GetTotalUserXp(user.Id));

                if (xp.Level >= 5 && du.Club == null)
                {
                    du.Club = new ClubInfo()
                    {
                        Name = clubName,
                        Discrim = uow.Clubs.GetNextDiscrim(clubName),
                        Owner = du,
                    };
                    uow.Clubs.Add(du.Club);
                    uow._context.SaveChanges();
                }
                else
                    return false;

                uow._context.Set<ClubApplicants>()
                    .RemoveRange(uow._context.Set<ClubApplicants>().Where(x => x.UserId == du.Id));
                club = du.Club;
                uow.Complete();
            }

            return true;
        }

        public ClubInfo GetClubByMember(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Clubs.GetByMember(user.Id);
            }
        }

        public bool SetClubIcon(ulong ownerUserId, string url)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(ownerUserId, set => set);

                if (club == null)
                    return false;

                club.ImageUrl = url;
                uow.Complete();
            }

            return true;
        }

        public bool GetClubByName(string clubName, out ClubInfo club)
        {
            club = null;
            var arr = clubName.Split('#');
            if (arr.Length < 2 || !int.TryParse(arr[arr.Length - 1], out var discrim))
                return false;

            //incase club has # in it
            var name = string.Concat(arr.Except(new[] { arr[arr.Length - 1] }));

            if (string.IsNullOrWhiteSpace(name))
                return false;

            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByName(name.Trim().ToLowerInvariant(), discrim);
                if (club == null)
                    return false;
                else
                    return true;
            }
        }

        public bool ApplyToClub(IUser user, ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                uow._context.SaveChanges();

                if (du.Club != null
                    || new LevelStats(uow.Xp.GetTotalUserXp(user.Id)).Level < club.MinimumLevelReq
                    || club.Bans.Any(x => x.UserId == du.Id)
                    || club.Applicants.Any(x => x.UserId == du.Id))
                {
                    //user banned or a member of a club, or already applied,
                    // or doesn't min minumum level requirement, can't apply
                    return false;
                }

                var app = new ClubApplicants
                {
                    ClubId = club.Id,
                    UserId = du.Id,
                };

                uow._context.Set<ClubApplicants>().Add(app);

                uow.Complete();
            }
            return true;
        }

        public bool AcceptApplication(ulong clubOwnerUserId, string userName, out DiscordUser discordUser)
        {
            discordUser = null;
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(clubOwnerUserId,
                    set => set.Include(x => x.Applicants)
                        .ThenInclude(x => x.Club)
                        .Include(x => x.Applicants)
                        .ThenInclude(x => x.User));
                if (club == null)
                    return false;

                var applicant = club.Applicants.FirstOrDefault(x => x.User.ToString().ToLowerInvariant() == userName.ToLowerInvariant());
                if (applicant == null)
                    return false;

                applicant.User.Club = club;
                club.Applicants.Remove(applicant);

                //remove that user's all other applications
                uow._context.Set<ClubApplicants>()
                    .RemoveRange(uow._context.Set<ClubApplicants>().Where(x => x.UserId == applicant.User.Id));

                discordUser = applicant.User;
                uow.Complete();
            }
            return true;
        }

        public ClubInfo GetBansAndApplications(ulong ownerUserId)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Clubs.GetByOwner(ownerUserId,
                    x => x.Include(y => y.Bans)
                          .ThenInclude(y => y.User)
                          .Include(y => y.Applicants)
                          .ThenInclude(y => y.User));
            }
        }

        public bool LeaveClub(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                if (du.Club == null || du.Club.OwnerId == du.Id)
                    return false;

                du.Club = null;
                uow.Complete();
            }
            return true;
        }

        public bool ChangeClubLevelReq(ulong userId, int level)
        {
            if (level < 5)
                return false;

            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(userId);
                if (club == null)
                    return false;

                club.MinimumLevelReq = level;
                uow.Complete();
            }

            return true;
        }

        public bool Disband(ulong userId, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwner(userId);
                if (club == null)
                    return false;

                uow.Clubs.Remove(club);
                uow.Complete();
            }
            return true;
        }

        public bool Ban(ulong ownerUserId, string userName, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwner(ownerUserId,
                    set => set.Include(x => x.Applicants)
                        .ThenInclude(x => x.User));
                if (club == null)
                    return false;

                var usr = club.Users.FirstOrDefault(x => x.ToString().ToLowerInvariant() == userName.ToLowerInvariant())
                    ?? club.Applicants.FirstOrDefault(x => x.User.ToString().ToLowerInvariant() == userName.ToLowerInvariant())?.User;
                if (usr == null)
                    return false;

                if (club.OwnerId == usr.Id) // can't ban the owner kek, whew
                    return false;

                club.Bans.Add(new ClubBans
                {
                    Club = club,
                    User = usr,
                });
                club.Users.Remove(usr);

                var app = club.Applicants.FirstOrDefault(x => x.UserId == usr.Id);
                if (app != null)
                    club.Applicants.Remove(app);

                uow.Complete();
            }

            return true;
        }

        public bool UnBan(ulong ownerUserId, string userName, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwner(ownerUserId,
                    set => set.Include(x => x.Bans)
                        .ThenInclude(x => x.User));
                if (club == null)
                    return false;

                var ban = club.Bans.FirstOrDefault(x => x.User.ToString().ToLowerInvariant() == userName.ToLowerInvariant());
                if (ban == null)
                    return false;

                club.Bans.Remove(ban);
                uow.Complete();
            }

            return true;
        }

        public bool Kick(ulong ownerUserId, string userName, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwner(ownerUserId);
                if (club == null)
                    return false;

                var usr = club.Users.FirstOrDefault(x => x.ToString().ToLowerInvariant() == userName.ToLowerInvariant());
                if (usr == null)
                    return false;

                if (club.OwnerId == usr.Id)
                    return false;

                club.Users.Remove(usr);
                var app = club.Applicants.FirstOrDefault(x => x.UserId == usr.Id);
                if (app != null)
                    club.Applicants.Remove(app);
                uow.Complete();
            }

            return true;
        }

        public ClubInfo[] GetClubLeaderboardPage(int page)
        {
            if (page < 0)
                throw new ArgumentOutOfRangeException(nameof(page));

            using (var uow = _db.UnitOfWork)
            {
                return uow.Clubs.GetClubLeaderboardPage(page);
            }
        }
    }
}