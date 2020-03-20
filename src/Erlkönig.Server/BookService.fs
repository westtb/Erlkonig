namespace Erlkönig.Server

open System
open System.IO
open Microsoft.AspNetCore.Hosting
open Bolero
open Bolero.Remoting
open Bolero.Remoting.Server
open PKHeX.Core
open Erlkönig

type BookService(ctx: IRemoteContext, env: IWebHostEnvironment) =
    inherit RemoteHandler<Client.Main.BookService>()

    let saved = FileUtil.GetSupportedFile(Path.Combine(env.ContentRootPath, "data/hg.sav"))

    let parsePokemon (pkm : PKM) : Client.Main.Pokemon =
        { species = pkm.Species
          speciesName = SpeciesName.GetSpeciesName(pkm.Species, int LanguageID.English)
          altform = 0
          nickname = if pkm.IsNicknamed then Some pkm.Nickname else None
          ability = Some pkm.Ability
          abilityName = Enum.GetName(typeof<Ability>, pkm.Ability)
          heldItem = Some pkm.HeldItem
          level = pkm.CurrentLevel
          data = pkm.Data }

    let pkmExists (pkm : PKM) = pkm.Species <> 0
    let parseCollect = Seq.filter pkmExists >> List.ofSeq >> List.map parsePokemon

    let readParty (saveFile : SaveFile) = saveFile.PartyData |> parseCollect

    let readBox (saveFile : SaveFile) = saveFile.GetBoxData >> parseCollect

    let readAllPokemon (saveFile : SaveFile) =
        Map.empty
        |> Map.add "Party" (readParty saveFile)
        |> List.fold (fun boxList boxId ->
            Map.add
                (saveFile.GetBoxName boxId)
                (readBox saveFile boxId)
                boxList
        )
        <| [ 0 .. (saveFile.BoxCount - 1)]

    override this.Handler =
        { readSave =
              ctx.Authorize <| fun () ->
                  async {
                      return if (saved :? SaveFile) then
                                 readAllPokemon (saved :?> SaveFile)
                             else
                                 Map.empty
                  }

          signIn =
              fun (username, password) ->
                  async {
                      if password = "password" then
                          do! ctx.HttpContext.AsyncSignIn(username, TimeSpan.FromDays(365.))
                          return Some username
                      else
                          return None
                  }

          signOut = fun () -> async { return! ctx.HttpContext.AsyncSignOut() }

          getUsername = ctx.Authorize <| fun () -> async { return ctx.HttpContext.User.Identity.Name } }
