import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_private_collection_controller.dart';

class ShopItemInventoryController
    extends ShopPrivateCollectionController<ShopInventoryItem> {
  ShopItemInventoryController({required ShopRepository repository})
      : super(
          loader: (accessToken) =>
              repository.getMyInventory(accessToken: accessToken),
          resourceLabel: '道具列表',
        );
}
