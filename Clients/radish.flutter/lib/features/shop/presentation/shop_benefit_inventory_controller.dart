import '../data/shop_models.dart';
import '../data/shop_repository.dart';
import 'shop_private_collection_controller.dart';

class ShopBenefitInventoryController
    extends ShopPrivateCollectionController<ShopUserBenefit> {
  ShopBenefitInventoryController({required ShopRepository repository})
      : super(
          loader: (accessToken) =>
              repository.getMyBenefits(accessToken: accessToken),
          resourceLabel: '权益列表',
        );
}
