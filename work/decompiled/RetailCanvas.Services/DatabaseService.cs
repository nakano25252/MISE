using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public sealed class DatabaseService
{
	private static readonly string[] LegacyCsvHeaders = new string[13]
	{
		"メーカー", "ブランド", "カテゴリ", "製品名", "型番", "JAN", "価格", "キャッチコピー", "特徴", "仕様",
		"画像パス", "URL", "タグ"
	};

	private static readonly string[] OfficialCsvHeaders = new string[19]
	{
		"メーカー", "ブランド", "カテゴリ", "製品名", "型番", "JANコード", "価格", "キャッチコピー", "製品特徴", "主な仕様",
		"コーデック／音声形式", "防水・防塵", "バッテリー", "重量", "画像パス", "素材フォルダ", "URL", "タグ", "販売トーク"
	};

	private static readonly string[] ExtendedCsvHeaders = OfficialCsvHeaders.Concat(new string[6] { "発売日", "カラーバリエーション", "注意事項", "セールスポイント", "素材画像役割", "情報元／更新状況" }).ToArray();

	private static readonly Dictionary<string, string[]> CsvAliases = new Dictionary<string, string[]>
	{
		["manufacturer"] = new string[2] { "メーカー", "製造元" },
		["brand"] = new string[1] { "ブランド" },
		["category"] = new string[2] { "カテゴリ", "カテゴリー" },
		["productName"] = new string[2] { "製品名", "商品名" },
		["modelNumber"] = new string[2] { "型番", "モデル番号" },
		["janCode"] = new string[6] { "JANコード", "JAN", "JAN Code", "JANCode", "jan_code", "商品JAN" },
		["price"] = new string[2] { "価格", "販売価格" },
		["catchCopy"] = new string[1] { "キャッチコピー" },
		["features"] = new string[3] { "製品特徴", "特徴", "商品特徴" },
		["specifications"] = new string[3] { "主な仕様", "仕様", "商品仕様" },
		["codec"] = new string[5] { "コーデック／音声形式", "コーデック/音声形式", "コーデック", "音声形式", "対応コーデック" },
		["waterproof"] = new string[5] { "防水・防塵", "防水/防塵", "防水防塵", "防水", "防塵" },
		["battery"] = new string[3] { "バッテリー", "再生時間", "電池" },
		["weight"] = new string[2] { "重量", "質量" },
		["imagePath"] = new string[3] { "画像パス", "画像", "商品画像" },
		["assetFolder"] = new string[2] { "素材フォルダ", "素材パス" },
		["url"] = new string[4] { "URL", "商品URL", "製品URL", "公式URL" },
		["tags"] = new string[2] { "タグ", "キーワード" },
		["salesTalk"] = new string[3] { "販売トーク", "セールストーク", "接客トーク" },
		["releaseDate"] = new string[2] { "発売日", "リリース日" },
		["colors"] = new string[3] { "カラーバリエーション", "カラー", "色" },
		["notes"] = new string[2] { "注意事項", "注意書き" },
		["salesPoints"] = new string[3] { "セールスポイント", "訴求カード", "訴求ポイント" },
		["assetRoles"] = new string[2] { "素材画像役割", "画像役割" },
		["sourceStatus"] = new string[4] { "情報元／更新状況", "情報元/更新状況", "更新状況", "情報元" }
	};

	private readonly string _connectionString = new SqliteConnectionStringBuilder
	{
		DataSource = AppPaths.DatabaseFile,
		Mode = SqliteOpenMode.ReadWriteCreate
	}.ToString();

	public DatabaseService()
	{
		Initialize();
	}

	private SqliteConnection Open()
	{
		SqliteConnection sqliteConnection = new SqliteConnection(_connectionString);
		sqliteConnection.Open();
		return sqliteConnection;
	}

	private void Initialize()
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "CREATE TABLE IF NOT EXISTS Products (\n    ProductId INTEGER PRIMARY KEY AUTOINCREMENT,\n    Manufacturer TEXT NOT NULL DEFAULT '', BrandName TEXT NOT NULL DEFAULT '',\n    Category TEXT NOT NULL DEFAULT 'その他', ProductName TEXT NOT NULL,\n    ModelNumber TEXT NOT NULL DEFAULT '', JANCode TEXT NOT NULL DEFAULT '',\n    ReleaseDate TEXT, Price REAL, Colors TEXT NOT NULL DEFAULT '',\n    ImagePath TEXT NOT NULL DEFAULT '', CatchCopy TEXT NOT NULL DEFAULT '',\n    Features TEXT NOT NULL DEFAULT '', Specifications TEXT NOT NULL DEFAULT '',\n    Notes TEXT NOT NULL DEFAULT '', Codec TEXT NOT NULL DEFAULT '',\n    Waterproof TEXT NOT NULL DEFAULT '', Battery TEXT NOT NULL DEFAULT '',\n    Weight TEXT NOT NULL DEFAULT '', Url TEXT NOT NULL DEFAULT '',\n    Tags TEXT NOT NULL DEFAULT '', SalesTalk TEXT NOT NULL DEFAULT '',\n    UpdatedAt TEXT NOT NULL\n);\nDROP INDEX IF EXISTS IX_Products_Brand_Model;\nCREATE INDEX IF NOT EXISTS IX_Products_Brand_Model\n    ON Products(BrandName, ModelNumber) WHERE ModelNumber <> '';";
		sqliteCommand.ExecuteNonQuery();
		EnsureColumn(sqliteConnection, "SalesPointData", "TEXT NOT NULL DEFAULT ''");
		EnsureColumn(sqliteConnection, "AssetFolderPath", "TEXT NOT NULL DEFAULT ''");
		EnsureColumn(sqliteConnection, "SourceStatus", "TEXT NOT NULL DEFAULT ''");
		EnsureColumn(sqliteConnection, "AssetRoleData", "TEXT NOT NULL DEFAULT ''");
		EnsureColumn(sqliteConnection, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
		EnsureColumn(sqliteConnection, "DeletedAt", "TEXT");
	}

	private static void EnsureColumn(SqliteConnection connection, string name, string definition)
	{
		using SqliteCommand sqliteCommand = connection.CreateCommand();
		sqliteCommand.CommandText = "PRAGMA table_info(Products)";
		using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
		bool flag = false;
		while (sqliteDataReader.Read())
		{
			if (string.Equals(sqliteDataReader["name"]?.ToString(), name, StringComparison.OrdinalIgnoreCase))
			{
				flag = true;
				break;
			}
		}
		sqliteDataReader.Close();
		if (flag)
		{
			return;
		}
		using SqliteCommand sqliteCommand2 = connection.CreateCommand();
		sqliteCommand2.CommandText = "ALTER TABLE Products ADD COLUMN " + name + " " + definition;
		sqliteCommand2.ExecuteNonQuery();
	}

	public List<ProductModel> Search(string? query = null, string? category = null, bool includeDeleted = false)
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		List<string> list = new List<string>();
		if (!includeDeleted)
		{
			list.Add("IsDeleted = 0");
		}
		if (!string.IsNullOrWhiteSpace(query))
		{
			list.Add("(ProductName LIKE $query OR ModelNumber LIKE $query OR BrandName LIKE $query OR JANCode LIKE $query OR Tags LIKE $query)");
			sqliteCommand.Parameters.AddWithValue("$query", "%" + query.Trim() + "%");
		}
		if (!string.IsNullOrWhiteSpace(category) && category != "すべて")
		{
			list.Add("Category = $category");
			sqliteCommand.Parameters.AddWithValue("$category", category);
		}
		sqliteCommand.CommandText = "SELECT * FROM Products" + ((list.Count == 0) ? string.Empty : (" WHERE " + string.Join(" AND ", list))) + " ORDER BY BrandName, ProductName";
		using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
		List<ProductModel> list2 = new List<ProductModel>();
		while (sqliteDataReader.Read())
		{
			list2.Add(ReadProduct(sqliteDataReader));
		}
		return list2;
	}

	public List<ProductModel> SearchDeleted()
	{
		return (from product in Search(null, null, includeDeleted: true)
			where product.IsDeleted
			orderby product.DeletedAt descending
			select product).ToList();
	}

	public long Save(ProductModel product)
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = ((product.ProductId == 0L) ? "INSERT INTO Products (Manufacturer, BrandName, Category, ProductName, ModelNumber, JANCode,\n  ReleaseDate, Price, Colors, ImagePath, CatchCopy, Features, Specifications, Notes, Codec,\n  Waterproof, Battery, Weight, Url, Tags, SalesTalk, SalesPointData, AssetFolderPath, SourceStatus, AssetRoleData, UpdatedAt)\nVALUES ($Manufacturer,$BrandName,$Category,$ProductName,$ModelNumber,$JANCode,$ReleaseDate,$Price,\n  $Colors,$ImagePath,$CatchCopy,$Features,$Specifications,$Notes,$Codec,$Waterproof,$Battery,\n  $Weight,$Url,$Tags,$SalesTalk,$SalesPointData,$AssetFolderPath,$SourceStatus,$AssetRoleData,$UpdatedAt);\nSELECT last_insert_rowid();" : "UPDATE Products SET Manufacturer=$Manufacturer, BrandName=$BrandName, Category=$Category,\n  ProductName=$ProductName, ModelNumber=$ModelNumber, JANCode=$JANCode, ReleaseDate=$ReleaseDate,\n  Price=$Price, Colors=$Colors, ImagePath=$ImagePath, CatchCopy=$CatchCopy, Features=$Features,\n  Specifications=$Specifications, Notes=$Notes, Codec=$Codec, Waterproof=$Waterproof,\n  Battery=$Battery, Weight=$Weight, Url=$Url, Tags=$Tags, SalesTalk=$SalesTalk, SalesPointData=$SalesPointData,\n  AssetFolderPath=$AssetFolderPath, SourceStatus=$SourceStatus, AssetRoleData=$AssetRoleData, UpdatedAt=$UpdatedAt\nWHERE ProductId=$ProductId; SELECT $ProductId;");
		AddParameters(sqliteCommand, product);
		return Convert.ToInt64(sqliteCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
	}

	public void Delete(long id)
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "DELETE FROM Products WHERE ProductId=$id";
		sqliteCommand.Parameters.AddWithValue("$id", id);
		sqliteCommand.ExecuteNonQuery();
	}

	public void MoveToTrash(IEnumerable<long> ids)
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		foreach (long item in ids.Distinct())
		{
			using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
			sqliteCommand.Transaction = sqliteTransaction;
			sqliteCommand.CommandText = "UPDATE Products SET IsDeleted=1, DeletedAt=$deleted WHERE ProductId=$id";
			sqliteCommand.Parameters.AddWithValue("$deleted", DateTime.Now.ToString("O"));
			sqliteCommand.Parameters.AddWithValue("$id", item);
			sqliteCommand.ExecuteNonQuery();
		}
		sqliteTransaction.Commit();
	}

	public void RestoreFromTrash(IEnumerable<long> ids)
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		foreach (long item in ids.Distinct())
		{
			using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
			sqliteCommand.Transaction = sqliteTransaction;
			sqliteCommand.CommandText = "UPDATE Products SET IsDeleted=0, DeletedAt=NULL WHERE ProductId=$id";
			sqliteCommand.Parameters.AddWithValue("$id", item);
			sqliteCommand.ExecuteNonQuery();
		}
		sqliteTransaction.Commit();
	}

	public void PermanentlyDelete(IEnumerable<long> ids)
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		foreach (long item in ids.Distinct())
		{
			using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
			sqliteCommand.Transaction = sqliteTransaction;
			sqliteCommand.CommandText = "DELETE FROM Products WHERE ProductId=$id AND IsDeleted=1";
			sqliteCommand.Parameters.AddWithValue("$id", item);
			sqliteCommand.ExecuteNonQuery();
		}
		sqliteTransaction.Commit();
	}

	public int ImportCsv(string path)
	{
		ProductCsvImportResult productCsvImportResult = ApplyImport(PreviewImportCsv(path), new ProductCsvImportOptions());
		return productCsvImportResult.Added + productCsvImportResult.Updated;
	}

	public ProductCsvPreview PreviewImportCsv(string path)
	{
		CsvReadResult csvReadResult = CsvService.ReadDetected(path);
		if (csvReadResult.Rows.Count == 0)
		{
			throw new InvalidDataException("CSVにデータがありません。");
		}
		List<string> headers = csvReadResult.Rows[0];
		Dictionary<string, int> dictionary = CreateHeaderMap(headers);
		if (dictionary.Count == 0)
		{
			throw new InvalidDataException("対応する商品列を認識できませんでした。");
		}
		ProductCsvPreview productCsvPreview = new ProductCsvPreview
		{
			SourcePath = path,
			EncodingName = csvReadResult.EncodingName
		};
		HashSet<int> used = dictionary.Values.ToHashSet();
		productCsvPreview.RecognizedHeaders.AddRange(from index in used
			orderby index
			select headers.ElementAtOrDefault(index) ?? string.Empty);
		productCsvPreview.UnknownHeaders.AddRange(headers.Where((string header, int index) => !used.Contains(index) && !string.IsNullOrWhiteSpace(header)));
		List<ProductModel> existing = Search();
		string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Environment.CurrentDirectory;
		for (int num = 1; num < csvReadResult.Rows.Count; num++)
		{
			List<string> list = csvReadResult.Rows[num];
			if (!list.All(string.IsNullOrWhiteSpace))
			{
				ProductCsvRow productCsvRow = new ProductCsvRow
				{
					RowNumber = num + 1
				};
				string field = GetField(list, dictionary, "price");
				decimal result;
				ProductModel productModel = new ProductModel
				{
					Manufacturer = GetField(list, dictionary, "manufacturer"),
					BrandName = GetField(list, dictionary, "brand"),
					Category = NormalizeCategory(GetField(list, dictionary, "category")),
					ProductName = GetField(list, dictionary, "productName"),
					ModelNumber = GetField(list, dictionary, "modelNumber"),
					JanCode = GetField(list, dictionary, "janCode"),
					Price = ((decimal.TryParse(field, NumberStyles.Any, CultureInfo.InvariantCulture, out result) || decimal.TryParse(field, NumberStyles.Any, CultureInfo.CurrentCulture, out result)) ? new decimal?(result) : ((decimal?)null)),
					CatchCopy = GetField(list, dictionary, "catchCopy"),
					Features = GetField(list, dictionary, "features"),
					Specifications = GetField(list, dictionary, "specifications"),
					Codec = GetField(list, dictionary, "codec"),
					Waterproof = GetField(list, dictionary, "waterproof"),
					Battery = GetField(list, dictionary, "battery"),
					Weight = GetField(list, dictionary, "weight"),
					ImagePath = ResolveCsvPath(GetField(list, dictionary, "imagePath"), baseDirectory),
					AssetFolderPath = ResolveCsvPath(GetField(list, dictionary, "assetFolder"), baseDirectory),
					Url = GetField(list, dictionary, "url"),
					Tags = NormalizeTags(GetField(list, dictionary, "tags")),
					SalesTalk = GetField(list, dictionary, "salesTalk")
				};
				string field2 = GetField(list, dictionary, "releaseDate");
				if (DateTime.TryParse(field2, CultureInfo.CurrentCulture, DateTimeStyles.None, out var result2) || DateTime.TryParse(field2, CultureInfo.InvariantCulture, DateTimeStyles.None, out result2))
				{
					productModel.ReleaseDate = result2;
				}
				productModel.Colors = GetField(list, dictionary, "colors");
				productModel.Notes = GetField(list, dictionary, "notes");
				productModel.SalesPointData = GetField(list, dictionary, "salesPoints");
				productModel.AssetRoleData = GetField(list, dictionary, "assetRoles");
				productModel.SourceStatus = GetField(list, dictionary, "sourceStatus");
				productCsvRow.Product = productModel;
				if (string.IsNullOrWhiteSpace(productModel.ProductName) && string.IsNullOrWhiteSpace(productModel.ModelNumber) && string.IsNullOrWhiteSpace(productModel.JanCode))
				{
					productCsvRow.IsSkipped = true;
					productCsvRow.Warnings.Add("製品名・型番・JANコードがすべて空欄のためスキップ");
				}
				if (!string.IsNullOrWhiteSpace(field) && !productModel.Price.HasValue)
				{
					productCsvRow.Warnings.Add("価格を数値として認識できません");
				}
				if (!string.IsNullOrWhiteSpace(productModel.Url) && (!Uri.TryCreate(productModel.Url, UriKind.Absolute, out Uri result3) || (result3.Scheme != Uri.UriSchemeHttp && result3.Scheme != Uri.UriSchemeHttps)))
				{
					productCsvRow.Warnings.Add("URL形式を確認してください");
				}
				if (!string.IsNullOrWhiteSpace(productModel.ImagePath) && !File.Exists(productModel.ImagePath))
				{
					productCsvRow.Warnings.Add("画像パスが存在しません");
				}
				if (!string.IsNullOrWhiteSpace(productModel.AssetFolderPath) && !Directory.Exists(productModel.AssetFolderPath))
				{
					productCsvRow.Warnings.Add("素材フォルダが存在しません");
				}
				productCsvRow.ExistingProductId = FindExisting(existing, productModel)?.ProductId ?? 0;
				productCsvPreview.Rows.Add(productCsvRow);
			}
		}
		return productCsvPreview;
	}

	public ProductCsvImportResult ApplyImport(ProductCsvPreview preview, ProductCsvImportOptions options)
	{
		ProductCsvImportResult productCsvImportResult = new ProductCsvImportResult();
		Dictionary<long, ProductModel> dictionary = Search().ToDictionary((ProductModel productModel) => productModel.ProductId);
		foreach (ProductCsvRow row in preview.Rows)
		{
			foreach (string warning in row.Warnings)
			{
				productCsvImportResult.Warnings.Add($"{row.RowNumber}行目：{warning}");
			}
			if (row.IsSkipped)
			{
				productCsvImportResult.Skipped++;
				continue;
			}
			if (row.ExistingProductId != 0L && options.DuplicateMode == ProductCsvDuplicateMode.Skip)
			{
				productCsvImportResult.Skipped++;
				continue;
			}
			ProductModel product = row.Product;
			if (row.ExistingProductId != 0L && options.DuplicateMode == ProductCsvDuplicateMode.Update && dictionary.TryGetValue(row.ExistingProductId, out var value))
			{
				MergeOfficialFields(value, product, options.ClearExistingOnBlank);
				Save(value);
				productCsvImportResult.Updated++;
				continue;
			}
			try
			{
				product.ProductId = 0L;
				Save(product);
				productCsvImportResult.Added++;
			}
			catch (Exception ex)
			{
				productCsvImportResult.Skipped++;
				productCsvImportResult.Warnings.Add($"{row.RowNumber}行目：登録できませんでした（{ex.Message}）");
			}
		}
		return productCsvImportResult;
	}

	public void ExportCsv(string path, ProductCsvExportFormat format = ProductCsvExportFormat.Official19)
	{
		List<IReadOnlyList<string>> list = new List<IReadOnlyList<string>>();
		List<IReadOnlyList<string>> list2 = list;
		list2.Add(format switch
		{
			ProductCsvExportFormat.Legacy13 => LegacyCsvHeaders, 
			ProductCsvExportFormat.Extended25 => ExtendedCsvHeaders, 
			_ => OfficialCsvHeaders, 
		});
		List<IReadOnlyList<string>> list3 = list;
		list3.AddRange(((IEnumerable<ProductModel>)Search()).Select((Func<ProductModel, IReadOnlyList<string>>)delegate(ProductModel p)
		{
			if (format == ProductCsvExportFormat.Legacy13)
			{
				return new List<string>
				{
					p.Manufacturer,
					p.BrandName,
					p.Category,
					p.ProductName,
					p.ModelNumber,
					p.JanCode,
					p.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
					p.CatchCopy,
					p.Features,
					p.Specifications,
					p.ImagePath,
					p.Url,
					p.Tags
				};
			}
			List<string> list4 = new List<string>
			{
				p.Manufacturer,
				p.BrandName,
				p.Category,
				p.ProductName,
				p.ModelNumber,
				p.JanCode,
				p.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
				p.CatchCopy,
				p.Features,
				p.Specifications,
				p.Codec,
				p.Waterproof,
				p.Battery,
				p.Weight,
				p.ImagePath,
				p.AssetFolderPath,
				p.Url,
				p.Tags,
				p.SalesTalk
			};
			if (format == ProductCsvExportFormat.Extended25)
			{
				list4.AddRange(new string[6]
				{
					p.ReleaseDate?.ToString("yyyy-MM-dd") ?? string.Empty,
					p.Colors,
					p.Notes,
					p.SalesPointData,
					p.AssetRoleData,
					p.SourceStatus
				});
			}
			return list4;
		}));
		CsvService.Write(path, list3);
	}

	private static Dictionary<string, int> CreateHeaderMap(IReadOnlyList<string> headers)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (KeyValuePair<string, string[]> csvAlias in CsvAliases)
		{
			csvAlias.Deconstruct(out var key, out var value);
			string key2 = key;
			foreach (string alias in value)
			{
				int num = Enumerable.Range(0, headers.Count).FirstOrDefault((int index) => string.Equals(headers[index]?.Trim(), alias, StringComparison.OrdinalIgnoreCase), -1);
				if (num >= 0)
				{
					dictionary[key2] = num;
					break;
				}
				string normalizedAlias = NormalizeHeader(alias);
				int num2 = Enumerable.Range(0, headers.Count).FirstOrDefault((int index) => NormalizeHeader(headers[index]) == normalizedAlias, -1);
				if (num2 >= 0)
				{
					dictionary[key2] = num2;
					break;
				}
			}
		}
		return dictionary;
	}

	private static string NormalizeHeader(string? value)
	{
		return new string((from character in (value ?? string.Empty).TrimStart('\ufeff').Normalize(NormalizationForm.FormKC)
			where !char.IsWhiteSpace(character)
			select character).ToArray()).ToUpperInvariant();
	}

	private static string GetField(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> map, string field)
	{
		if (!map.TryGetValue(field, out var value) || value < 0 || value >= row.Count)
		{
			return string.Empty;
		}
		return (row[value] ?? string.Empty).Trim();
	}

	private static ProductModel? FindExisting(IEnumerable<ProductModel> existing, ProductModel product)
	{
		if (!string.IsNullOrWhiteSpace(product.JanCode))
		{
			ProductModel productModel = existing.FirstOrDefault((ProductModel item) => string.Equals(item.JanCode, product.JanCode, StringComparison.OrdinalIgnoreCase));
			if (productModel != null)
			{
				return productModel;
			}
		}
		if (!string.IsNullOrWhiteSpace(product.ModelNumber))
		{
			return existing.FirstOrDefault((ProductModel item) => string.Equals(item.ModelNumber, product.ModelNumber, StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(product.BrandName) || string.Equals(item.BrandName, product.BrandName, StringComparison.OrdinalIgnoreCase)));
		}
		return null;
	}

	private static string ResolveCsvPath(string value, string baseDirectory)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		try
		{
			return Path.IsPathRooted(value) ? Path.GetFullPath(value) : Path.GetFullPath(value, baseDirectory);
		}
		catch
		{
			return value;
		}
	}

	private static string NormalizeCategory(string value)
	{
		switch (NormalizeHeader(value))
		{
		case "TWS":
		case "オープンイヤー":
		case "完全ワイヤレス":
			return "TWS";
		case "BTHP":
		case "ヘッドホン":
			return "ヘッドホン";
		case "BTSP":
		case "スピーカー":
		case "BLUETOOTHスピーカー":
			return "スピーカー";
		case "BAR":
		case "サウンドバー":
			return "サウンドバー";
		default:
			return string.IsNullOrWhiteSpace(value) ? "未分類" : value.Trim();
		}
	}

	private static string NormalizeTags(string value)
	{
		return string.Join(",", value.Split(new char[6] { ',', '、', ';', '；', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct<string>(StringComparer.OrdinalIgnoreCase));
	}

	private static void MergeOfficialFields(ProductModel target, ProductModel source, bool clearOnBlank)
	{
		target.Manufacturer = Pick(target.Manufacturer, source.Manufacturer, clearOnBlank);
		target.BrandName = Pick(target.BrandName, source.BrandName, clearOnBlank);
		target.Category = Pick(target.Category, source.Category, clearOnBlank);
		target.ProductName = Pick(target.ProductName, source.ProductName, clearOnBlank);
		target.ModelNumber = Pick(target.ModelNumber, source.ModelNumber, clearOnBlank);
		target.JanCode = Pick(target.JanCode, source.JanCode, clearOnBlank);
		if (clearOnBlank || source.Price.HasValue)
		{
			target.Price = source.Price;
		}
		target.CatchCopy = Pick(target.CatchCopy, source.CatchCopy, clearOnBlank);
		target.Features = Pick(target.Features, source.Features, clearOnBlank);
		target.Specifications = Pick(target.Specifications, source.Specifications, clearOnBlank);
		target.Codec = Pick(target.Codec, source.Codec, clearOnBlank);
		target.Waterproof = Pick(target.Waterproof, source.Waterproof, clearOnBlank);
		target.Battery = Pick(target.Battery, source.Battery, clearOnBlank);
		target.Weight = Pick(target.Weight, source.Weight, clearOnBlank);
		target.ImagePath = Pick(target.ImagePath, source.ImagePath, clearOnBlank);
		target.AssetFolderPath = Pick(target.AssetFolderPath, source.AssetFolderPath, clearOnBlank);
		target.Url = Pick(target.Url, source.Url, clearOnBlank);
		target.Tags = Pick(target.Tags, source.Tags, clearOnBlank);
		target.SalesTalk = Pick(target.SalesTalk, source.SalesTalk, clearOnBlank);
		if (clearOnBlank || source.ReleaseDate.HasValue)
		{
			target.ReleaseDate = source.ReleaseDate;
		}
		target.Colors = Pick(target.Colors, source.Colors, clearOnBlank);
		target.Notes = Pick(target.Notes, source.Notes, clearOnBlank);
		target.SalesPointData = Pick(target.SalesPointData, source.SalesPointData, clearOnBlank);
		target.AssetRoleData = Pick(target.AssetRoleData, source.AssetRoleData, clearOnBlank);
		target.SourceStatus = Pick(target.SourceStatus, source.SourceStatus, clearOnBlank);
		static string Pick(string current, string incoming, bool clear)
		{
			if (!clear && string.IsNullOrWhiteSpace(incoming))
			{
				return current;
			}
			return incoming;
		}
	}

	private static void AddParameters(SqliteCommand command, ProductModel p)
	{
		command.Parameters.AddWithValue("$ProductId", p.ProductId);
		command.Parameters.AddWithValue("$Manufacturer", p.Manufacturer);
		command.Parameters.AddWithValue("$BrandName", p.BrandName);
		command.Parameters.AddWithValue("$Category", p.Category);
		command.Parameters.AddWithValue("$ProductName", p.ProductName);
		command.Parameters.AddWithValue("$ModelNumber", p.ModelNumber);
		command.Parameters.AddWithValue("$JANCode", p.JanCode);
		command.Parameters.AddWithValue("$ReleaseDate", ((object)p.ReleaseDate?.ToString("yyyy-MM-dd")) ?? ((object)DBNull.Value));
		command.Parameters.AddWithValue("$Price", ((object)p.Price) ?? DBNull.Value);
		command.Parameters.AddWithValue("$Colors", p.Colors);
		command.Parameters.AddWithValue("$ImagePath", p.ImagePath);
		command.Parameters.AddWithValue("$CatchCopy", p.CatchCopy);
		command.Parameters.AddWithValue("$Features", p.Features);
		command.Parameters.AddWithValue("$Specifications", p.Specifications);
		command.Parameters.AddWithValue("$Notes", p.Notes);
		command.Parameters.AddWithValue("$Codec", p.Codec);
		command.Parameters.AddWithValue("$Waterproof", p.Waterproof);
		command.Parameters.AddWithValue("$Battery", p.Battery);
		command.Parameters.AddWithValue("$Weight", p.Weight);
		command.Parameters.AddWithValue("$Url", p.Url);
		command.Parameters.AddWithValue("$Tags", p.Tags);
		command.Parameters.AddWithValue("$SalesTalk", p.SalesTalk);
		command.Parameters.AddWithValue("$SalesPointData", p.SalesPointData);
		command.Parameters.AddWithValue("$AssetFolderPath", p.AssetFolderPath);
		command.Parameters.AddWithValue("$SourceStatus", p.SourceStatus);
		command.Parameters.AddWithValue("$AssetRoleData", p.AssetRoleData);
		command.Parameters.AddWithValue("$UpdatedAt", DateTime.Now.ToString("O"));
	}

	private static ProductModel ReadProduct(SqliteDataReader r)
	{
		DateTime result;
		decimal result2;
		DateTime result3;
		DateTime result4;
		return new ProductModel
		{
			ProductId = r.GetInt64(r.GetOrdinal("ProductId")),
			Manufacturer = (r["Manufacturer"]?.ToString() ?? string.Empty),
			BrandName = (r["BrandName"]?.ToString() ?? string.Empty),
			Category = (r["Category"]?.ToString() ?? string.Empty),
			ProductName = (r["ProductName"]?.ToString() ?? string.Empty),
			ModelNumber = (r["ModelNumber"]?.ToString() ?? string.Empty),
			JanCode = (r["JANCode"]?.ToString() ?? string.Empty),
			ReleaseDate = (DateTime.TryParse(r["ReleaseDate"]?.ToString(), out result) ? new DateTime?(result) : ((DateTime?)null)),
			Price = (decimal.TryParse(r["Price"]?.ToString(), out result2) ? new decimal?(result2) : ((decimal?)null)),
			Colors = (r["Colors"]?.ToString() ?? string.Empty),
			ImagePath = (r["ImagePath"]?.ToString() ?? string.Empty),
			CatchCopy = (r["CatchCopy"]?.ToString() ?? string.Empty),
			Features = (r["Features"]?.ToString() ?? string.Empty),
			Specifications = (r["Specifications"]?.ToString() ?? string.Empty),
			Notes = (r["Notes"]?.ToString() ?? string.Empty),
			Codec = (r["Codec"]?.ToString() ?? string.Empty),
			Waterproof = (r["Waterproof"]?.ToString() ?? string.Empty),
			Battery = (r["Battery"]?.ToString() ?? string.Empty),
			Weight = (r["Weight"]?.ToString() ?? string.Empty),
			Url = (r["Url"]?.ToString() ?? string.Empty),
			Tags = (r["Tags"]?.ToString() ?? string.Empty),
			SalesTalk = (r["SalesTalk"]?.ToString() ?? string.Empty),
			SalesPointData = (r["SalesPointData"]?.ToString() ?? string.Empty),
			AssetFolderPath = (r["AssetFolderPath"]?.ToString() ?? string.Empty),
			SourceStatus = (r["SourceStatus"]?.ToString() ?? string.Empty),
			AssetRoleData = (r["AssetRoleData"]?.ToString() ?? string.Empty),
			IsDeleted = (Convert.ToInt32(r["IsDeleted"], CultureInfo.InvariantCulture) != 0),
			DeletedAt = (DateTime.TryParse(r["DeletedAt"]?.ToString(), out result3) ? new DateTime?(result3) : ((DateTime?)null)),
			UpdatedAt = (DateTime.TryParse(r["UpdatedAt"]?.ToString(), out result4) ? result4 : DateTime.Now)
		};
	}
}
